import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';
import { toAuthError } from '../../core/auth/auth-error';
import { AuthStore } from '../../core/auth/auth.store';
import { AdminHeaderComponent } from '../admin/admin-header.component';

@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, AdminHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex flex-1 flex-col bg-slate-50 dark:bg-slate-950">
      <app-admin-header />

      <main class="mx-auto w-full max-w-2xl flex-1 px-4 py-12">
        <h1 class="text-2xl font-semibold text-slate-900 dark:text-slate-100">Profile</h1>
        <p class="mt-1 text-sm text-slate-600 dark:text-slate-400">
          Signed in as
          <span class="font-medium text-slate-900 dark:text-slate-100">{{
            store.displayName()
          }}</span>
        </p>

        <section
          class="mt-8 rounded-xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900"
          aria-labelledby="display-name-title"
        >
          <h2
            id="display-name-title"
            class="text-lg font-semibold text-slate-900 dark:text-slate-100"
          >
            Display name
          </h2>
          <p class="mt-1 text-sm text-slate-600 dark:text-slate-400">
            The name shown around the app. Leave it blank to fall back to your email.
          </p>

          @if (nameDone()) {
            <div
              role="status"
              class="mt-4 rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300"
            >
              Display name updated.
            </div>
          }

          @if (nameError(); as msg) {
            <div
              role="alert"
              class="mt-4 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-300"
            >
              {{ msg }}
            </div>
          }

          <form [formGroup]="nameForm" (ngSubmit)="saveName()" novalidate class="mt-6">
            <fieldset [disabled]="nameSaving()" class="space-y-4">
              <div>
                <label
                  for="displayName"
                  class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300"
                  >Display name</label
                >
                <input
                  id="displayName"
                  type="text"
                  autocomplete="name"
                  formControlName="displayName"
                  [attr.aria-invalid]="showNameError() ? 'true' : null"
                  [attr.aria-describedby]="showNameError() ? 'displayName-error' : null"
                  class="block w-full rounded-md border border-slate-300 px-3 py-2 text-slate-900 shadow-sm focus:border-slate-900 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:border-slate-100 dark:focus:ring-slate-100"
                />
                @if (showNameError()) {
                  <p id="displayName-error" class="mt-1 text-sm text-red-700 dark:text-red-400">
                    Display name must be 80 characters or fewer.
                  </p>
                }
              </div>
              <button
                type="submit"
                class="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:bg-slate-400 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300 dark:focus:ring-slate-100 dark:focus:ring-offset-slate-950 dark:disabled:bg-slate-700"
              >
                {{ nameSaving() ? 'Saving…' : 'Save' }}
              </button>
            </fieldset>
          </form>
        </section>

        <section
          class="mt-8 rounded-xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900"
          aria-labelledby="change-password-title"
        >
          <h2
            id="change-password-title"
            class="text-lg font-semibold text-slate-900 dark:text-slate-100"
          >
            Change password
          </h2>
          <p class="mt-1 text-sm text-slate-600 dark:text-slate-400">
            Enter your current password, then a new password of at least 12 characters.
          </p>

          @if (done()) {
            <div
              role="status"
              class="mt-4 rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300"
            >
              Your password has been changed.
            </div>
          }

          @if (error(); as msg) {
            <div
              role="alert"
              class="mt-4 rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-300"
            >
              {{ msg }}
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="mt-6">
            <fieldset [disabled]="pending()" class="space-y-4">
              <div>
                <label
                  for="currentPassword"
                  class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300"
                  >Current password</label
                >
                <div class="relative">
                  <input
                    id="currentPassword"
                    [type]="currentVisible() ? 'text' : 'password'"
                    autocomplete="current-password"
                    formControlName="currentPassword"
                    class="block w-full rounded-md border border-slate-300 px-3 py-2 pr-20 text-slate-900 shadow-sm focus:border-slate-900 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:border-slate-100 dark:focus:ring-slate-100"
                  />
                  <button
                    type="button"
                    (click)="toggleCurrent()"
                    [attr.aria-pressed]="currentVisible()"
                    aria-label="Show or hide current password"
                    class="absolute inset-y-0 right-2 my-1 rounded px-2 text-xs font-medium text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:text-slate-300 dark:hover:bg-slate-700 dark:focus:ring-slate-100"
                  >
                    {{ currentVisible() ? 'Hide' : 'Show' }}
                  </button>
                </div>
              </div>

              <div>
                <label
                  for="newPassword"
                  class="mb-1 block text-sm font-medium text-slate-700 dark:text-slate-300"
                  >New password</label
                >
                <div class="relative">
                  <input
                    id="newPassword"
                    [type]="newVisible() ? 'text' : 'password'"
                    autocomplete="new-password"
                    formControlName="newPassword"
                    [attr.aria-invalid]="showNewError() ? 'true' : null"
                    [attr.aria-describedby]="showNewError() ? 'newPassword-error' : null"
                    class="block w-full rounded-md border border-slate-300 px-3 py-2 pr-20 text-slate-900 shadow-sm focus:border-slate-900 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:border-slate-100 dark:focus:ring-slate-100"
                  />
                  <button
                    type="button"
                    (click)="toggleNew()"
                    [attr.aria-pressed]="newVisible()"
                    aria-label="Show or hide new password"
                    class="absolute inset-y-0 right-2 my-1 rounded px-2 text-xs font-medium text-slate-700 hover:bg-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:text-slate-300 dark:hover:bg-slate-700 dark:focus:ring-slate-100"
                  >
                    {{ newVisible() ? 'Hide' : 'Show' }}
                  </button>
                </div>
                @if (showNewError()) {
                  <p id="newPassword-error" class="mt-1 text-sm text-red-700 dark:text-red-400">
                    Password must be at least 12 characters.
                  </p>
                }
              </div>

              <button
                type="submit"
                class="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:bg-slate-400 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300 dark:focus:ring-slate-100 dark:focus:ring-offset-slate-950 dark:disabled:bg-slate-700"
              >
                {{ pending() ? 'Saving…' : 'Change password' }}
              </button>
            </fieldset>
          </form>
        </section>

        <section
          class="mt-8 rounded-xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900"
          aria-labelledby="roles-title"
        >
          <h2 id="roles-title" class="text-lg font-semibold text-slate-900 dark:text-slate-100">
            Your roles
          </h2>
          <p class="mt-1 text-sm text-slate-600 dark:text-slate-400">
            The access you currently hold. Read-only — ask a league or club admin to change it.
          </p>

          @if (grants(); as g) {
            @if (hasAnyGrant()) {
              <ul class="mt-4 space-y-2">
                @if (g.systemAdmin) {
                  <li class="flex items-center gap-3 text-sm">
                    <span
                      class="inline-block w-28 shrink-0 font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
                      >System</span
                    >
                    <span class="text-slate-900 dark:text-slate-100">System administrator</span>
                  </li>
                }
                @for (l of g.leagueAdmin; track l.id) {
                  <li class="flex items-center gap-3 text-sm">
                    <span
                      class="inline-block w-28 shrink-0 font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
                      >League admin</span
                    >
                    <span class="text-slate-900 dark:text-slate-100">{{ l.name }}</span>
                  </li>
                }
                @for (c of g.clubAdmin; track c.id) {
                  <li class="flex items-center gap-3 text-sm">
                    <span
                      class="inline-block w-28 shrink-0 font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
                      >Club admin</span
                    >
                    <span class="text-slate-900 dark:text-slate-100">{{ c.name }}</span>
                  </li>
                }
                @for (h of g.sessionHost; track h.id) {
                  <li class="flex items-center gap-3 text-sm">
                    <span
                      class="inline-block w-28 shrink-0 font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
                      >Session host</span
                    >
                    <span class="text-slate-900 dark:text-slate-100">{{ h.name }}</span>
                  </li>
                }
              </ul>
            } @else {
              <p class="mt-4 text-sm text-slate-500 dark:text-slate-400">
                You don't hold any role grants yet.
              </p>
            }
          }
        </section>
      </main>
    </div>
  `,
})
export default class ProfilePage {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AuthApi);
  protected readonly store = inject(AuthStore);

  protected readonly currentVisible = signal(false);
  protected readonly newVisible = signal(false);
  protected readonly pending = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(12)]],
  });

  protected readonly nameForm = this.fb.nonNullable.group({
    displayName: [this.store.user()?.displayName ?? '', [Validators.maxLength(80)]],
  });
  protected readonly nameSaving = signal(false);
  protected readonly nameDone = signal(false);
  protected readonly nameError = signal<string | null>(null);

  protected readonly grants = toSignal(this.api.myGrants());
  protected readonly hasAnyGrant = computed(() => {
    const g = this.grants();
    return (
      !!g &&
      (g.systemAdmin ||
        g.leagueAdmin.length > 0 ||
        g.clubAdmin.length > 0 ||
        g.sessionHost.length > 0)
    );
  });

  protected toggleCurrent(): void {
    this.currentVisible.update((v) => !v);
  }

  protected toggleNew(): void {
    this.newVisible.update((v) => !v);
  }

  protected showNewError(): boolean {
    const c = this.form.controls.newPassword;
    return c.invalid && (c.dirty || c.touched);
  }

  protected showNameError(): boolean {
    const c = this.nameForm.controls.displayName;
    return c.invalid && (c.dirty || c.touched);
  }

  protected async saveName(): Promise<void> {
    if (this.nameForm.invalid) {
      this.nameForm.markAllAsTouched();
      return;
    }
    this.nameSaving.set(true);
    this.nameError.set(null);
    this.nameDone.set(false);
    const raw = this.nameForm.controls.displayName.value.trim();
    const ok = await this.store.updateDisplayName(raw.length ? raw : null);
    this.nameSaving.set(false);
    if (ok) {
      this.nameDone.set(true);
    } else {
      this.nameError.set(this.store.error()?.message ?? 'Could not update your display name.');
    }
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.pending.set(true);
    this.error.set(null);
    this.done.set(false);
    const { currentPassword, newPassword } = this.form.getRawValue();
    try {
      await firstValueFrom(this.api.changePassword(currentPassword, newPassword));
      this.done.set(true);
      this.form.reset();
    } catch (err) {
      this.error.set(toAuthError(err, 'me').message);
    } finally {
      this.pending.set(false);
    }
  }
}
