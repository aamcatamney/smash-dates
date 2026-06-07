import { Routes } from '@angular/router';

// Anonymous, read-only public pages — deliberately not behind authGuard.
export const PUBLIC_ROUTES: Routes = [
  {
    path: '',
    title: 'Leagues · smash-dates',
    loadComponent: () => import('./public-leagues.page'),
  },
  {
    path: 'leagues/:leagueId',
    title: 'League · smash-dates',
    loadComponent: () => import('./public-league.page'),
  },
];
