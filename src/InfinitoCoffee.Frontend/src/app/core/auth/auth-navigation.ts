import { inject } from '@angular/core';
import { RedirectFunction } from '@angular/router';

import { AuthenticationState } from './authentication-state.service';
import { UserRole } from './models/authenticated-user.model';

export function getRoleHome(role: UserRole): string {
  return role === 'Kitchen' ? '/kitchen' : '/orders/new';
}

export const roleHomeRedirect: RedirectFunction = () => {
  const state = inject(AuthenticationState);
  const role = state.role();

  return role ? getRoleHome(role) : '/login';
};