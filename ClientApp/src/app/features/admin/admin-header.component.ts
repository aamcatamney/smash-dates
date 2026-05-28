import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-admin-header',
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="border-b border-slate-200 bg-white">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
        <a
          [routerLink]="['/']"
          class="font-mono text-sm font-semibold tracking-wide text-slate-900 hover:underline focus-visible:outline-2 focus-visible:outline-slate-900"
          >smash-dates</a
        >
        <nav class="flex items-center gap-4">
          <a
            [routerLink]="['/admin/leagues']"
            routerLinkActive="text-slate-900 underline"
            class="font-mono text-xs uppercase tracking-wider text-slate-600 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-slate-900"
            >Leagues</a
          >
          <a
            [routerLink]="['/admin/clubs']"
            routerLinkActive="text-slate-900 underline"
            class="font-mono text-xs uppercase tracking-wider text-slate-600 hover:text-slate-900 focus-visible:outline-2 focus-visible:outline-slate-900"
            >Clubs</a
          >
          <span class="text-sm text-slate-600" aria-live="polite">
            Hi, <span class="font-medium text-slate-900">{{ store.displayName() }}</span>
          </span>
          <button
            type="button"
            (click)="logout()"
            [disabled]="store.pending()"
            class="rounded-md border border-slate-300 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 shadow-sm hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 focus:ring-offset-2 disabled:opacity-50"
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
