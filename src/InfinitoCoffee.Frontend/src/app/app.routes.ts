import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'kitchen',
  },
  {
    path: 'kitchen',
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
    loadComponent: () => import('./features/order-entry/ui/order-entry-page.component')
      .then((module) => module.OrderEntryPageComponent),
  },
];
