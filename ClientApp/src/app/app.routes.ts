import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';

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
    canActivate: [authGuard],
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  {
    path: 'profile',
    title: 'Profile · smash-dates',
    canActivate: [authGuard],
    loadComponent: () => import('./features/profile/profile.page'),
  },
  {
    // Anonymous public view — no authGuard.
    path: 'public',
    loadChildren: () => import('./features/public/public.routes').then((m) => m.PUBLIC_ROUTES),
  },
  {
    path: '',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
