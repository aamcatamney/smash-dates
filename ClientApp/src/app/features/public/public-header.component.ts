import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeToggleComponent } from '../../shared/theme-toggle.component';

// Minimal header for the anonymous public pages — no admin nav, just the wordmark, the theme
// toggle and a way in to sign in.
@Component({
  selector: 'app-public-header',
  imports: [RouterLink, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header
      class="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800 sm:px-6"
    >
      <a
        routerLink="/public"
        class="font-mono text-sm font-semibold text-slate-900 dark:text-slate-100"
      >
        smash-dates
      </a>
      <div class="flex items-center gap-3">
        <app-theme-toggle />
        <a
          routerLink="/login"
          class="rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
        >
          Sign in
        </a>
      </div>
    </header>
  `,
})
export class PublicHeaderComponent {}
