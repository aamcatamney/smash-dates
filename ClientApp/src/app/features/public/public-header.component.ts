import { ChangeDetectionStrategy, Component } from '@angular/core';
import { NgOptimizedImage } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ThemeToggleComponent } from '../../shared/theme-toggle.component';

// Minimal header for the anonymous public pages — no admin nav, just the wordmark, the theme
// toggle and a way in to sign in.
@Component({
  selector: 'app-public-header',
  imports: [RouterLink, ThemeToggleComponent, NgOptimizedImage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header
      class="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800 sm:px-6"
    >
      <a
        routerLink="/public"
        class="flex items-center gap-2 font-mono text-sm font-semibold text-slate-900 dark:text-slate-100"
      >
        <img ngSrc="favicon.svg" width="20" height="20" priority alt="" />
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
        <a
          routerLink="/register"
          class="rounded-md bg-slate-900 px-3 py-1.5 font-mono text-xs text-amber-300 hover:bg-slate-800 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300"
        >
          Create account
        </a>
      </div>
    </header>
  `,
})
export class PublicHeaderComponent {}
