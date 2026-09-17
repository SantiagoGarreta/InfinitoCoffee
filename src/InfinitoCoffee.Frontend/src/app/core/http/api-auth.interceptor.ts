import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';

import { AuthenticationState } from '../auth/authentication-state.service';
import { CsrfTokenStore } from '../auth/csrf-token.store';
import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { OrdersRealtimeService } from '../realtime/orders-realtime.service';

const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

export const apiAuthInterceptor: HttpInterceptorFn = (request, next) => {
  const runtimeConfig = inject(APP_RUNTIME_CONFIG);
  const csrfTokenStore = inject(CsrfTokenStore);
  const authenticationState = inject(AuthenticationState);
  const ordersRealtimeService = inject(OrdersRealtimeService);
  const router = inject(Router);

  if (!isApiRequest(request.url, runtimeConfig.apiBaseUrl)) {
    return next(request);
  }

  const token = csrfTokenStore.getToken();
  const authenticatedRequest = request.clone({
    withCredentials: true,
    setHeaders: unsafeMethods.has(request.method.toUpperCase()) && token
      ? { 'X-XSRF-TOKEN': token }
      : {},
  });

  return next(authenticatedRequest).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)
        || error.status !== 401
        || isExpectedUnauthorizedRequest(request.url)) {
        return throwError(() => error);
      }

      authenticationState.clearUser();
      csrfTokenStore.clear();

      return from(ordersRealtimeService.stop()).pipe(
        switchMap(async () => {
          if (isPrivateRoute(router.url)) {
            await router.navigate(['/login'], { replaceUrl: true });
          }

          throw error;
        }),
      );
    }),
  );
};

function isApiRequest(requestUrl: string, apiBaseUrl: string): boolean {
  try {
    const apiUrl = new URL(apiBaseUrl);
    const url = new URL(requestUrl, apiUrl);
    const basePath = apiUrl.pathname.replace(/\/$/, '');
    return url.origin === apiUrl.origin
      && (basePath.length === 0 || url.pathname === basePath || url.pathname.startsWith(`${basePath}/`));
  } catch {
    return false;
  }
}

function isExpectedUnauthorizedRequest(requestUrl: string): boolean {
  try {
    const path = new URL(requestUrl, 'http://localhost').pathname;
    return path.endsWith('/api/auth/me') || path.endsWith('/api/auth/login');
  } catch {
    return false;
  }
}

function isPrivateRoute(routerUrl: string): boolean {
  const path = routerUrl.split(/[?#]/, 1)[0];
  return path !== '/login' && path !== '/pickup' && path !== '/';
}
