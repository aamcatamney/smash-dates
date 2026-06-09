import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../../core/auth/auth.api';
import { ThemeToggleComponent } from '../../shared/theme-toggle.component';

type VerifyStatus = 'verifying' | 'ok' | 'failed' | 'no-token';

@Component({
  selector: 'app-verify-email-page',
  imports: [RouterLink, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main
      class="relative flex-1 flex items-center justify-center bg-slate-50 dark:bg-slate-950 px-4 py-12"
    >
      <div class="absolute right-4 top-4"><app-theme-toggle /></div>
      <section
        class="w-full max-w-md bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 rounded-xl shadow-sm p-8"
        aria-labelledby="verify-title"
      >
        <h1
          id="verify-title"
          class="text-2xl font-semibold text-slate-900 dark:text-slate-100 mb-4"
        >
          Email verification
        </h1>

        @switch (status()) {
          @case ('verifying') {
            <p role="status" class="text-sm text-slate-600 dark:text-slate-400">
              Verifying your email…
            </p>
          }
          @case ('ok') {
            <div
              role="status"
              class="rounded-md border border-emerald-200 dark:border-emerald-900 bg-emerald-50 dark:bg-emerald-950 px-4 py-3 text-sm text-emerald-800 dark:text-emerald-300"
            >
              Your email is verified. You can now sign in.
            </div>
            <p class="mt-6 text-sm text-slate-600 dark:text-slate-400">
              <a
                routerLink="/login"
                class="font-medium text-slate-900 dark:text-slate-100 underline"
                >Continue to sign in</a
              >
            </p>
          }
          @case ('failed') {
            <div
              role="alert"
              class="rounded-md border border-red-200 dark:border-red-900 bg-red-50 dark:bg-red-950 px-4 py-3 text-sm text-red-800 dark:text-red-300"
            >
              This verification link is invalid or has expired.
            </div>
            <p class="mt-6 text-sm text-slate-600 dark:text-slate-400">
              Sign in to request a fresh link —
              <a
                routerLink="/login"
                class="font-medium text-slate-900 dark:text-slate-100 underline"
                >go to sign in</a
              >.
            </p>
          }
          @case ('no-token') {
            <div
              role="alert"
              class="rounded-md border border-red-200 dark:border-red-900 bg-red-50 dark:bg-red-950 px-4 py-3 text-sm text-red-800 dark:text-red-300"
            >
              This link is missing its verification token.
            </div>
            <p class="mt-6 text-sm text-slate-600 dark:text-slate-400">
              <a
                routerLink="/login"
                class="font-medium text-slate-900 dark:text-slate-100 underline"
                >Back to sign in</a
              >
            </p>
          }
        }
      </section>
    </main>
  `,
  styleUrl: './auth-shell.css',
})
export default class VerifyEmailPage implements OnInit {
  private readonly api = inject(AuthApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly status = signal<VerifyStatus>('verifying');

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.status.set('no-token');
      return;
    }
    void this.verify(token);
  }

  private async verify(token: string): Promise<void> {
    try {
      await firstValueFrom(this.api.verifyEmail(token));
      this.status.set('ok');
    } catch {
      this.status.set('failed');
    }
  }
}
