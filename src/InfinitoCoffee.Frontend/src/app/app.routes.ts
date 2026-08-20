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
    path: 'pickup',
    loadComponent: () => import('./features/pickup-display/ui/pickup-display-page.component')
      .then((module) => module.PickupDisplayPageComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/authenticated-shell/authenticated-shell.component')
      .then((module) => module.AuthenticatedShellComponent),
    children: [
      {
        path: 'orders/new',
        canActivate: [roleGuard('Administrator', 'Cashier')],
        loadComponent: () => import('./features/order-entry/ui/order-entry-page.component')
          .then((module) => module.OrderEntryPageComponent),
      },
      {
        path: 'kitchen',
        canActivate: [roleGuard('Administrator', 'Cashier', 'Kitchen')],
        loadComponent: () => import('./features/kitchen-display/ui/kitchen-display-page.component')
          .then((module) => module.KitchenDisplayPageComponent),
      },
      {
        path: 'admin',
        canActivate: [roleGuard('Administrator')],
        children: [
          {
            path: '',
            pathMatch: 'full',
            loadComponent: () => import('./features/admin/home/admin-home-page.component')
              .then((module) => module.AdminHomePageComponent),
          },
          {
            path: 'products',
            loadComponent: () => import('./features/admin/products/ui/admin-products-page.component')
              .then((module) => module.AdminProductsPageComponent),
          },
          {
            path: 'categories',
            loadComponent: () => import('./features/admin/categories/ui/admin-categories-page.component')
              .then((module) => module.AdminCategoriesPageComponent),
          },
          {
            path: 'users',
            loadComponent: () => import('./features/admin/users/ui/admin-users-page.component')
              .then((module) => module.AdminUsersPageComponent),
          },
          {
            path: 'results',
            loadComponent: () => import('./features/admin/results/ui/admin-results-page.component')
              .then((module) => module.AdminResultsPageComponent),
          },
        ],
      },
    ],
  },
  {
    path: '**',
    redirectTo: roleHomeRedirect,
  },
];
