import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { ThemeToggleComponent } from '../../shared/theme-toggle.component';

@Component({
  selector: 'app-admin-header',
  imports: [RouterLink, RouterLinkActive, ThemeToggleComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="border-b border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <div
        class="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-3"
      >
        <a
          [routerLink]="['/']"
          class="font-mono text-sm font-semibold tracking-wide text-slate-900 hover:underline focus-visible:outline-2 focus-visible:outline-slate-900 dark:text-slate-100 dark:focus-visible:outline-slate-100"
          >smash-dates</a
        >
        <nav class="flex flex-wrap items-center gap-3 sm:gap-4">
          <a
            [routerLink]="['/admin/leagues']"
            routerLinkActive="text-slate-900 underline dark:text-slate-100"
            class="font-mono text-xs uppercase tracking-wider text-slate-600 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-slate-900 dark:text-slate-400 dark:hover:text-slate-100 dark:focus-visible:outline-slate-100"
            >Leagues</a
          >
          <a
            [routerLink]="['/admin/clubs']"
            routerLinkActive="text-slate-900 underline dark:text-slate-100"
            class="font-mono text-xs uppercase tracking-wider text-slate-600 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-slate-900 dark:text-slate-400 dark:hover:text-slate-100 dark:focus-visible:outline-slate-100"
            >Clubs</a
          >
          <span class="hidden text-sm text-slate-600 dark:text-slate-400 sm:inline" aria-live="polite">
            Hi, <span class="font-medium text-slate-900 dark:text-slate-100">{{ store.displayName() }}</span>
          </span>
          <app-theme-toggle />
          <button
            type="button"
            (click)="logout()"
            [disabled]="store.pending()"
            class="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:opacity-50 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-300 dark:shadow-none dark:hover:bg-slate-800 dark:focus:ring-slate-100 dark:focus:ring-offset-slate-950"
          >
            {{ store.pending() ? 'Signing out…' : 'Sign out' }}
          </button>
        </nav>
      </div>
    </header>
  `,
})
export class AdminHeaderComponent {
  protected readonly store = inject(AuthStore);
  private readonly router = inject(Router);

  protected async logout(): Promise<void> {
    await this.store.logout();
    this.router.navigate(['/login']);
  }
}
