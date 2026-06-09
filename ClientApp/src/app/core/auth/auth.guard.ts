import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthStore } from './auth.store';

function safeReturnUrl(value: string | null | undefined): string {
  if (!value) return '/';
  if (!value.startsWith('/') || value.startsWith('//')) return '/';
  return value;
}

export const authGuard: CanActivateFn = (_route, state): boolean | UrlTree => {
  const store = inject(AuthStore);
  const router = inject(Router);

  if (store.isAuthed()) return true;

  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: state.url },
  });
};

export const redirectIfAuthedGuard: CanActivateFn = (route): boolean | UrlTree => {
  const store = inject(AuthStore);
  const router = inject(Router);

  if (!store.isAuthed()) return true;

  const returnUrl = safeReturnUrl(route.queryParamMap.get('returnUrl'));
  return router.parseUrl(returnUrl);
};

// The app root: authenticated users get their dashboard; anonymous visitors are sent to the
// public, login-free landing rather than bounced to the sign-in page.
export const landingGuard: CanActivateFn = (): boolean | UrlTree => {
  const store = inject(AuthStore);
  const router = inject(Router);

  if (store.isAuthed()) return true;

  return router.createUrlTree(['/public']);
};
