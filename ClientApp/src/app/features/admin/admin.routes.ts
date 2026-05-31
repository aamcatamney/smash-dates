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
  {
    path: 'leagues/:id/admins',
    title: 'League admins · smash-dates',
    loadComponent: () => import('./league-admins.page'),
  },
  {
    path: 'clubs',
    title: 'Clubs · smash-dates',
    loadComponent: () => import('./clubs-list.page'),
  },
  {
    path: 'clubs/:id',
    title: 'Club · smash-dates',
    loadComponent: () => import('./club-detail.page'),
  },
  {
    path: 'clubs/:id/pegboard/:sessionId',
    title: 'Pegboard · smash-dates',
    loadComponent: () => import('./pegboard-board.page'),
  },
];
