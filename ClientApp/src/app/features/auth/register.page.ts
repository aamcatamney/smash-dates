import { ChangeDetectionStrategy, Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { ThemeToggleComponent } from '../../shared/theme-toggle.component';

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="relative min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-950 px-4 py-12">
      <div class="absolute right-4 top-4"><app-theme-toggle /></div>
      <section
        class="w-full max-w-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm p-8"
        aria-labelledby="register-title"
      >
        <h1 id="register-title" class="text-2xl font-semibold text-slate-900 dark:text-slate-100 mb-1">Create account</h1>
        <p class="text-sm text-slate-600 dark:text-slate-400 mb-6">Set up a new account to get started.</p>

        @if (store.error(); as err) {
          <div
            role="alert"
            class="mb-4 rounded-md border border-red-200 dark:border-red-900 bg-red-50 dark:bg-red-950 px-3 py-2 text-sm text-red-800 dark:text-red-300"
          >
            {{ err.message }}
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <fieldset [disabled]="store.pending()" class="space-y-4">
            <div>
              <label for="email" class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Email</label>
              <input
                #emailInput
                id="email"
                type="email"
                autocomplete="email"
                formControlName="email"
                [attr.aria-invalid]="showError('email') ? 'true' : null"
                [attr.aria-describedby]="showError('email') ? 'email-error' : null"
                class="block w-full rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 text-slate-900 dark:text-slate-100 dark:bg-slate-800 shadow-sm focus:border-slate-900 dark:focus:border-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100"
              />
              @if (showError('email')) {
                <p id="email-error" class="mt-1 text-sm text-red-700 dark:text-red-400">Enter a valid email.</p>
              }
            </div>

            <div>
              <label for="displayName" class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">
                Display name <span class="text-slate-400 dark:text-slate-500 font-normal">(optional)</span>
              </label>
              <input
                id="displayName"
                type="text"
                autocomplete="name"
                formControlName="displayName"
                class="block w-full rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 text-slate-900 dark:text-slate-100 dark:bg-slate-800 shadow-sm focus:border-slate-900 dark:focus:border-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100"
              />
            </div>

            <div>
              <div class="flex items-baseline justify-between mb-1">
                <label for="password" class="block text-sm font-medium text-slate-700 dark:text-slate-300">Password</label>
                <span class="text-xs text-slate-500 dark:text-slate-400">12+ characters</span>
              </div>
              <div class="relative">
                <input
                  id="password"
                  [type]="passwordVisible() ? 'text' : 'password'"
                  autocomplete="new-password"
                  formControlName="password"
                  [attr.aria-invalid]="showError('password') ? 'true' : null"
                  [attr.aria-describedby]="showError('password') ? 'password-error' : null"
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
              @if (showError('password')) {
                <p id="password-error" class="mt-1 text-sm text-red-700 dark:text-red-400">Password must be at least 12 characters.</p>
              }
            </div>

            <button
              type="submit"
              class="w-full rounded-md bg-slate-900 dark:bg-amber-400 px-3 py-2 text-sm font-medium text-white dark:text-slate-900 shadow-sm hover:bg-slate-800 dark:hover:bg-amber-300 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 focus:ring-offset-2 dark:focus:ring-offset-slate-950 disabled:bg-slate-400 dark:disabled:bg-slate-700"
            >
              {{ store.pending() ? 'Creating account…' : 'Create account' }}
            </button>
          </fieldset>
        </form>

        <p class="mt-6 text-sm text-slate-600 dark:text-slate-400">
          Already have an account?
          <a routerLink="/login" [queryParams]="passThroughReturnUrl()" class="font-medium text-slate-900 dark:text-slate-100 underline">Sign in</a>.
        </p>
      </section>
    </main>
  `,
  styleUrl: './auth-shell.css',
})
export default class RegisterPage implements OnInit {
  protected readonly store = inject(AuthStore);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly emailInput = viewChild<ElementRef<HTMLInputElement>>('emailInput');

  protected readonly passwordVisible = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    displayName: [''],
    password: ['', [Validators.required, Validators.minLength(12)]],
  });

  protected readonly passThroughReturnUrl = computed(() => {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    return returnUrl ? { returnUrl } : {};
  });

  ngOnInit(): void {
    this.store.clearError();
    queueMicrotask(() => this.emailInput()?.nativeElement.focus());
  }

  protected togglePassword(): void {
    this.passwordVisible.update((v) => !v);
  }

  protected showError(name: 'email' | 'password'): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.dirty || c.touched);
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const ok = await this.store.register({
      email: raw.email,
      password: raw.password,
      displayName: raw.displayName.trim() === '' ? null : raw.displayName.trim(),
    });
    if (ok) {
      const target = safeReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'));
      this.router.navigateByUrl(target);
    }
  }
}

function safeReturnUrl(value: string | null): string {
  if (!value) return '/';
  if (!value.startsWith('/') || value.startsWith('//')) return '/';
  return value;
}
