import { isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { APP_RUNTIME_CONFIG } from '../config/app-runtime-config';
import { OrdersRealtimeService } from '../realtime/orders-realtime.service';
import { AuthenticationState } from './authentication-state.service';
import { CsrfTokenStore } from './csrf-token.store';
import { AuthenticatedUser } from './models/authenticated-user.model';

interface CsrfTokenResponse {
  token: string;
}

export class InvalidCredentialsError extends Error {
  constructor() {
    super('Invalid credentials.');
    this.name = 'InvalidCredentialsError';
  }
}

@Injectable({ providedIn: 'root' })
export class AuthenticationService {
  private readonly httpClient = inject(HttpClient);
  private readonly runtimeConfig = inject(APP_RUNTIME_CONFIG);
  private readonly authenticationState = inject(AuthenticationState);
  private readonly csrfTokenStore = inject(CsrfTokenStore);
  private readonly ordersRealtimeService = inject(OrdersRealtimeService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly authBaseUrl = `${this.runtimeConfig.apiBaseUrl}/api/auth`;
  private initializePromise: Promise<void> | null = null;

  initialize(): Promise<void> {
    if (this.initializePromise) {
      return this.initializePromise;
    }

    this.initializePromise = this.initializeCore();
    return this.initializePromise;
  }

  async login(username: string, password: string): Promise<AuthenticatedUser> {
    await this.refreshCsrfToken();

    let user: AuthenticatedUser;
    try {
      user = await firstValueFrom(this.httpClient.post<AuthenticatedUser>(`${this.authBaseUrl}/login`, {
        username,
        password,
      }));
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        throw new InvalidCredentialsError();
      }

      throw error;
    }

    this.csrfTokenStore.clear();
    await this.refreshCsrfToken();
    this.authenticationState.setUser(user);
    return user;
  }

  async logout(): Promise<void> {
    if (!this.csrfTokenStore.getToken()) {
      await this.refreshCsrfToken();
    }

    try {
      await firstValueFrom(this.httpClient.post<void>(`${this.authBaseUrl}/logout`, {}));
    } catch (error: unknown) {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        throw error;
      }
    }

    await this.clearSession();
  }

  async refreshCurrentUser(): Promise<AuthenticatedUser | null> {
    try {
      const user = await firstValueFrom(this.httpClient.get<AuthenticatedUser>(`${this.authBaseUrl}/me`));
      this.authenticationState.setUser(user);
      return user;
    } catch (error: unknown) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        this.authenticationState.clearUser();
        return null;
      }

      throw error;
    }
  }

  async refreshCsrfToken(): Promise<void> {
    const response = await firstValueFrom(this.httpClient.get<CsrfTokenResponse>(`${this.authBaseUrl}/csrf`));
    this.csrfTokenStore.setToken(response.token);
  }

  async clearSession(): Promise<void> {
    await this.ordersRealtimeService.stop();
    this.authenticationState.clearUser();
    this.csrfTokenStore.clear();
  }

  private async initializeCore(): Promise<void> {
    if (!this.isBrowser) {
      this.authenticationState.markInitialized();
      return;
    }

    try {
      const user = await this.refreshCurrentUser();
      if (user === null) {
        this.csrfTokenStore.clear();
      }

      await this.refreshCsrfToken();
    } catch {
      this.authenticationState.clearUser();
      this.csrfTokenStore.clear();
    } finally {
      this.authenticationState.markInitialized();
    }
  }
}
