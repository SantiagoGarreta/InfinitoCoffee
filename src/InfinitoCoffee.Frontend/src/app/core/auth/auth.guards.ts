import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthenticationState } from './authentication-state.service';
import { getRoleHome } from './auth-navigation';
import { UserRole } from './models/authenticated-user.model';

export const authGuard: CanActivateFn = () => {
  const state = inject(AuthenticationState);
  const router = inject(Router);
  return state.isAuthenticated() ? true : router.createUrlTree(['/login']);
};

export const guestGuard: CanActivateFn = () => {
  const state = inject(AuthenticationState);
  const router = inject(Router);
  const role = state.role();
  return role ? router.createUrlTree([getRoleHome(role)]) : true;
};

export function roleGuard(...allowedRoles: UserRole[]): CanActivateFn {
  return () => {
    const state = inject(AuthenticationState);
    const router = inject(Router);
    const role = state.role();

    if (!role) {
      return router.createUrlTree(['/login']);
    }

    return allowedRoles.includes(role)
      ? true
      : router.createUrlTree([getRoleHome(role)]);
  };
}

export const roleHomeGuard: CanActivateFn = () => {
  const state = inject(AuthenticationState);
  const router = inject(Router);
  const role = state.role();
  return router.createUrlTree([role ? getRoleHome(role) : '/login']);
};
