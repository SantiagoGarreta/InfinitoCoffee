import { Routes } from '@angular/router';

import { authGuard, guestGuard, roleGuard, roleHomeGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    canActivate: [roleHomeGuard],
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
    canActivate: [roleHomeGuard],
  },
];
