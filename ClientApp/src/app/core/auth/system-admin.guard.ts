import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { AuthStore } from './auth.store';

export const systemAdminGuard: CanActivateFn = (): boolean | UrlTree => {
  const store = inject(AuthStore);
  const router = inject(Router);

  if (store.isSystemAdmin()) return true;
  return router.createUrlTree(['/']);
};
