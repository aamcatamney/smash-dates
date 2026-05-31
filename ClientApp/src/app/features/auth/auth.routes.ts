import { Routes } from '@angular/router';
import { redirectIfAuthedGuard } from '../../core/auth/auth.guard';

export const AUTH_ROUTES: Routes = [
  {
    path: 'login',
    title: 'Sign in · smash-dates',
    canActivate: [redirectIfAuthedGuard],
    loadComponent: () => import('./login.page'),
  },
  {
    path: 'register',
    title: 'Create account · smash-dates',
    canActivate: [redirectIfAuthedGuard],
    loadComponent: () => import('./register.page'),
  },
  {
    path: 'forgot-password',
    title: 'Reset password · smash-dates',
    canActivate: [redirectIfAuthedGuard],
    loadComponent: () => import('./forgot-password.page'),
  },
  {
    // Reached from an emailed link; no auth guard so a signed-out user can complete it.
    path: 'reset-password',
    title: 'Choose a new password · smash-dates',
    loadComponent: () => import('./reset-password.page'),
  },
  {
    path: 'verify-email',
    title: 'Verify email · smash-dates',
    loadComponent: () => import('./verify-email.page'),
  },
];
