import { Routes } from '@angular/router';

import { authGuard, guestGuard, roleGuard } from './core/auth/auth.guards';
import { roleHomeRedirect } from './core/auth/auth-navigation';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: roleHomeRedirect,
  },
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/ui/login-page.component')
      .then((module) => module.LoginPageComponent),
  },
  {
    path: 'kitchen',
    canActivate: [authGuard, roleGuard('Administrator', 'Kitchen')],
    loadComponent: () => import('./features/kitchen-display/ui/kitchen-display-page.component')
      .then((module) => module.KitchenDisplayPageComponent),
  },
  {
    path: 'pickup',
    loadComponent: () => import('./features/pickup-display/ui/pickup-display-page.component')
      .then((module) => module.PickupDisplayPageComponent),
  },
  {
    path: 'orders/new',
    canActivate: [authGuard, roleGuard('Administrator', 'Cashier')],
    loadComponent: () => import('./features/order-entry/ui/order-entry-page.component')
      .then((module) => module.OrderEntryPageComponent),
  },
  {
    path: '**',
    redirectTo: roleHomeRedirect,
  },
];
