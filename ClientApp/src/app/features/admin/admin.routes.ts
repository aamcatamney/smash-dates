import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'leagues',
  },
  {
    path: 'leagues',
    title: 'Leagues · smash-dates',
    loadComponent: () => import('./leagues-list.page'),
  },
  {
    path: 'leagues/:id',
    title: 'League · smash-dates',
    loadComponent: () => import('./league-detail.page'),
  },
];
