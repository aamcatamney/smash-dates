import { computed, inject } from '@angular/core';
import { signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AuthApi, LoginPayload, RegisterPayload, isVerificationRequired } from './auth.api';
import { AuthError, toAuthError } from './auth-error';
import { AuthStatus, AuthenticatedUser, MyGrants } from './user.model';
import { patchState } from '@ngrx/signals';

export type RegisterOutcome = 'authed' | 'verify' | 'error';

interface AuthState {
  user: AuthenticatedUser | null;
  grants: MyGrants | null;
  status: AuthStatus;
  error: AuthError | null;
  pending: boolean;
}

const initialState: AuthState = {
  user: null,
  grants: null,
  status: 'unknown',
  error: null,
  pending: false,
};

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    isAuthed: computed(() => store.status() === 'authed'),
    isResolved: computed(() => store.status() !== 'unknown'),
    isSystemAdmin: computed(() => store.user()?.isSystemAdmin ?? false),
    displayName: computed(() => {
      const user = store.user();
      if (!user) return null;
      return user.displayName ?? user.email;
    }),
  })),
  withMethods((store, api = inject(AuthApi)) => {
    // The signed-in user's role grants drive which admin controls the UI shows. Fetched
    // alongside the session; failures degrade to "no grants" (controls stay hidden) rather
    // than blocking sign-in.
    const loadGrants = async (): Promise<MyGrants | null> => {
      try {
        return await firstValueFrom(api.myGrants());
      } catch {
        return null;
      }
    };

    return {
      async loadMe(): Promise<void> {
        try {
          const user = await firstValueFrom(api.me());
          patchState(store, { user, grants: await loadGrants(), status: 'authed', error: null });
        } catch {
          patchState(store, { user: null, grants: null, status: 'anonymous', error: null });
        }
      },
      async login(payload: LoginPayload): Promise<boolean> {
        patchState(store, { pending: true, error: null });
        try {
          const user = await firstValueFrom(api.login(payload));
          patchState(store, {
            user,
            grants: await loadGrants(),
            status: 'authed',
            error: null,
            pending: false,
          });
          return true;
        } catch (error) {
          patchState(store, {
            error: toAuthError(error, 'login'),
            pending: false,
          });
          return false;
        }
      },
      async register(payload: RegisterPayload): Promise<RegisterOutcome> {
        patchState(store, { pending: true, error: null });
        try {
          const result = await firstValueFrom(api.register(payload));
          if (isVerificationRequired(result)) {
            // Non-bootstrap sign-up: no session yet, user must verify their email first.
            patchState(store, { error: null, pending: false });
            return 'verify';
          }
          patchState(store, {
            user: result,
            grants: await loadGrants(),
            status: 'authed',
            error: null,
            pending: false,
          });
          return 'authed';
        } catch (error) {
          patchState(store, {
            error: toAuthError(error, 'register'),
            pending: false,
          });
          return 'error';
        }
      },
      async logout(): Promise<void> {
        patchState(store, { pending: true });
        try {
          await firstValueFrom(api.logout());
        } catch (error) {
          console.warn('Logout server call failed; clearing local state anyway.', error);
        } finally {
          patchState(store, {
            user: null,
            grants: null,
            status: 'anonymous',
            error: null,
            pending: false,
          });
        }
      },
      clearError(): void {
        patchState(store, { error: null });
      },
      markAnonymous(): void {
        patchState(store, { user: null, grants: null, status: 'anonymous' });
      },
      // Role checks for UI gating (SystemAdmin can manage anything). The backend enforces
      // the same rules regardless; these only decide which controls are shown.
      isLeagueAdmin(leagueId: string): boolean {
        if (store.isSystemAdmin()) return true;
        return store.grants()?.leagueAdmin.some((g) => g.id === leagueId) ?? false;
      },
      isClubAdmin(clubId: string): boolean {
        if (store.isSystemAdmin()) return true;
        return store.grants()?.clubAdmin.some((g) => g.id === clubId) ?? false;
      },
      // A pegboard session may be run by a club admin or a dedicated SessionHost.
      isSessionRunner(clubId: string): boolean {
        if (store.isSystemAdmin()) return true;
        const grants = store.grants();
        if (!grants) return false;
        return (
          grants.clubAdmin.some((g) => g.id === clubId) ||
          grants.sessionHost.some((g) => g.id === clubId)
        );
      },
    };
  }),
);

export type AuthStore = InstanceType<typeof AuthStore>;
