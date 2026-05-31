import { ChangeDetectionStrategy, Component, ElementRef, OnInit, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';
import { ThemeToggleComponent } from '../../shared/theme-toggle.component';

@Component({
  selector: 'app-forgot-password-page',
  imports: [ReactiveFormsModule, RouterLink, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="relative min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-950 px-4 py-12">
      <div class="absolute right-4 top-4"><app-theme-toggle /></div>
      <section
        class="w-full max-w-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm p-8"
        aria-labelledby="forgot-title"
      >
        <h1 id="forgot-title" class="text-2xl font-semibold text-slate-900 dark:text-slate-100 mb-1">Reset password</h1>

        @if (sent()) {
          <p class="text-sm text-slate-600 dark:text-slate-400 mb-6">Check your inbox.</p>
          <div
            role="status"
            class="rounded-md border border-emerald-200 dark:border-emerald-900 bg-emerald-50 dark:bg-emerald-950 px-4 py-3 text-sm text-emerald-800 dark:text-emerald-300"
          >
            If an account exists for that email, we've sent a link to reset your password. The link expires in one hour.
          </div>
        } @else {
          <p class="text-sm text-slate-600 dark:text-slate-400 mb-6">Enter your email and we'll send you a reset link.</p>
          <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
            <fieldset [disabled]="pending()" class="space-y-4">
              <div>
                <label for="email" class="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-1">Email</label>
                <input
                  #emailInput
                  id="email"
                  type="email"
                  autocomplete="email"
                  formControlName="email"
                  [attr.aria-invalid]="showError() ? 'true' : null"
                  [attr.aria-describedby]="showError() ? 'email-error' : null"
                  class="block w-full rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 text-slate-900 dark:text-slate-100 dark:bg-slate-800 shadow-sm focus:border-slate-900 dark:focus:border-slate-100 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100"
                />
                @if (showError()) {
                  <p id="email-error" class="mt-1 text-sm text-red-700 dark:text-red-400">Enter a valid email.</p>
                }
              </div>
              <button
                type="submit"
                class="w-full rounded-md bg-slate-900 dark:bg-amber-400 px-3 py-2 text-sm font-medium text-white dark:text-slate-900 shadow-sm hover:bg-slate-800 dark:hover:bg-amber-300 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 focus:ring-offset-2 dark:focus:ring-offset-slate-950 disabled:bg-slate-400 dark:disabled:bg-slate-700"
              >
                {{ pending() ? 'Sending…' : 'Send reset link' }}
              </button>
            </fieldset>
          </form>
        }

        <p class="mt-6 text-sm text-slate-600 dark:text-slate-400">
          <a routerLink="/login" class="font-medium text-slate-900 dark:text-slate-100 underline">Back to sign in</a>
        </p>
      </section>
    </main>
  `,
  styleUrl: './auth-shell.css',
})
export default class ForgotPasswordPage implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(AuthApi);
  private readonly emailInput = viewChild<ElementRef<HTMLInputElement>>('emailInput');

  protected readonly pending = signal(false);
  protected readonly sent = signal(false);
  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
  });

  ngOnInit(): void {
    queueMicrotask(() => this.emailInput()?.nativeElement.focus());
  }

  protected showError(): boolean {
    const c = this.form.controls.email;
    return c.invalid && (c.dirty || c.touched);
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.pending.set(true);
    try {
      await firstValueFrom(this.api.forgotPassword(this.form.controls.email.value.trim()));
    } catch {
      // Endpoint never reveals whether the email exists; show the same confirmation regardless.
    }
    this.pending.set(false);
    this.sent.set(true);
  }
}
