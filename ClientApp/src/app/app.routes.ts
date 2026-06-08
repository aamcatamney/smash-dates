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
  // Admin pages live at the top level (no /admin prefix). Each is guarded individually so
  // there is only ever one empty-path route group (the auth routes below) to fall through to.
  {
    path: 'leagues',
    title: 'Leagues · smash-dates',
    canActivate: [authGuard],
    loadComponent: () => import('./features/admin/leagues-list.page'),
  },
  {
    path: 'leagues/:id',
    title: 'League · smash-dates',
    canActivate: [authGuard],
    loadComponent: () => import('./features/admin/league-detail.page'),
  },
  {
    path: 'clubs',
    title: 'Clubs · smash-dates',
    canActivate: [authGuard],
    loadComponent: () => import('./features/admin/clubs-list.page'),
  },
  {
    path: 'clubs/:id',
    title: 'Club · smash-dates',
    canActivate: [authGuard],
    loadComponent: () => import('./features/admin/club-detail.page'),
  },
  {
    path: 'clubs/:id/pegboard/:sessionId',
    title: 'Pegboard · smash-dates',
    canActivate: [authGuard],
    loadComponent: () => import('./features/admin/pegboard-board.page'),
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
  // Back-compat: the admin pages used to live under /admin/*. Redirect old links/bookmarks.
  { path: 'admin', pathMatch: 'full', redirectTo: '' },
  { path: 'admin/leagues', pathMatch: 'full', redirectTo: 'leagues' },
  { path: 'admin/leagues/:id', redirectTo: 'leagues/:id' },
  { path: 'admin/clubs', pathMatch: 'full', redirectTo: 'clubs' },
  { path: 'admin/clubs/:id/pegboard/:sessionId', redirectTo: 'clubs/:id/pegboard/:sessionId' },
  { path: 'admin/clubs/:id', redirectTo: 'clubs/:id' },
  {
    path: '',
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
