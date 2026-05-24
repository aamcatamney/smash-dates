import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { systemAdminGuard } from './core/auth/system-admin.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Home · smash-dates',
    canActivate: [authGuard],
    loadComponent: () => import('./features/landing/landing.page'),
  },
  {
    path: 'admin',
    canActivate: [authGuard, systemAdminGuard],
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  {
    path: '',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
