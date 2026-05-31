import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';
import { toAuthError } from '../../core/auth/auth-error';
import { ThemeToggleComponent } from '../../shared/theme-toggle.component';

@Component({
  selector: 'app-reset-password-page',
  imports: [ReactiveFormsModule, RouterLink, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="relative min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-950 px-4 py-12">
      <div class="absolute right-4 top-4"><app-theme-toggle /></div>
      <section
        class="w-full max-w-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm p-8"
        aria-labelledby="reset-title"
      >
        <h1 id="reset-title" class="text-2xl font-semibold text-slate-900 dark:text-slate-100 mb-1">Choose a new password</h1>

        @if (done()) {
          <p class="text-sm text-slate-600 dark:text-slate-400 mb-6">All set.</p>
          <div
            role="status"
            class="rounded-md border border-emerald-200 dark:border-emerald-900 bg-emerald-50 dark:bg-emerald-950 px-4 py-3 text-sm text-emerald-800 dark:text-emerald-300"
          >
            Your password has been reset. You can now sign in with your new password.
          </div>
          <p class="mt-6 text-sm text-slate-600 dark:text-slate-400">
            <a routerLink="/login" class="font-medium text-slate-900 dark:text-slate-100 underline">Continue to sign in</a>
          </p>
        } @else if (!token) {
          <p class="text-sm text-slate-600 dark:text-slate-400 mb-6">This link is missing its reset token.</p>
          <div role="alert" class="rounded-md border border-red-200 dark:border-red-900 bg-red-50 dark:bg-red-950 px-4 py-3 text-sm text-red-800 dark:text-red-300">
            Open the link from your reset email, or request a new one.
          </div>
          <p class="mt-6 text-sm text-slate-600 dark:text-slate-400">
            <a routerLink="/forgot-password" class="font-medium text-slate-900 dark:text-slate-100 underline">Request a reset link</a>
          </p>
        } @else {
          <p class="text-sm text-slate-600 dark:text-slate-400 mb-6">Pick a password with at least 12 characters.</p>

          @if (error(); as msg) {
            <div role="alert" class="mb-4 rounded-md border border-red-200 dark:border-red-900 bg-red-50 dark:bg-red-950 px-3 py-2 text-sm text-red-800 dark:text-red-300">
              {{ msg }}
            </div>
          }

          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <fieldset [disabled]="pending()" class="space-y-4">
              <div>
                <label for="password" class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">New password</label>
                <div class="relative">
                  <input
                    #passwordInput
                    id="password"
                    [type]="passwordVisible() ? 'text' : 'password'"
                    autocomplete="new-password"
                    formControlName="password"
                    [attr.aria-invalid]="showError() ? 'true' : null"
                    [attr.aria-describedby]="showError() ? 'password-error' : null"
                    class="block w-full rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 pr-20 text-slate-900 dark:text-slate-100 dark:bg-slate-800 shadow-sm focus:border-slate-900 dark:focus:border-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100"
                  />
                  <button
                    type="button"
                    (click)="togglePassword()"
                    [attr.aria-pressed]="passwordVisible()"
                    aria-label="Show or hide password"
                    class="absolute inset-y-0 right-2 my-1 px-2 rounded text-xs font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-700 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100"
                  >
                    {{ passwordVisible() ? 'Hide' : 'Show' }}
                  </button>
                </div>
                @if (showError()) {
                  <p id="password-error" class="mt-1 text-sm text-red-700 dark:text-red-400">Password must be at least 12 characters.</p>
                }
              </div>
              <button
                type="submit"
                class="w-full rounded-md bg-slate-900 dark:bg-amber-400 px-3 py-2 text-sm font-medium text-white dark:text-slate-900 shadow-sm hover:bg-slate-800 dark:hover:bg-amber-300 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 focus:ring-offset-2 dark:focus:ring-offset-slate-950 disabled:bg-slate-400 dark:disabled:bg-slate-700"
              >
                {{ pending() ? 'Saving…' : 'Reset password' }}
              </button>
            </fieldset>
          </form>
        }
      </section>
    </main>
  `,
  styleUrl: './auth-shell.css',
})
export default class ResetPasswordPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AuthApi);
  private readonly route = inject(ActivatedRoute);
  private readonly passwordInput = viewChild<ElementRef<HTMLInputElement>>('passwordInput');

  protected readonly token = this.route.snapshot.queryParamMap.get('token');
  protected readonly passwordVisible = signal(false);
  protected readonly pending = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = this.fb.nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(12)]],
  });

  ngOnInit(): void {
    queueMicrotask(() => this.passwordInput()?.nativeElement.focus());
  }

  protected togglePassword(): void {
    this.passwordVisible.update((v) => !v);
  }

  protected showError(): boolean {
    const c = this.form.controls.password;
    return c.invalid && (c.dirty || c.touched);
  }

  protected async submit(): Promise<void> {
    if (!this.token || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.pending.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.resetPassword(this.token, this.form.controls.password.value));
      this.done.set(true);
    } catch (err) {
      const e = toAuthError(err, 'register');
      this.error.set(
        e.kind === 'validation'
          ? 'This reset link is invalid or has expired. Request a new one.'
          : e.message,
      );
    } finally {
      this.pending.set(false);
    }
  }
}
