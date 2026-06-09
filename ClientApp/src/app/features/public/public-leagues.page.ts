import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PublicApi, PublicLeague } from './public.api';
import { PublicHeaderComponent } from './public-header.component';

// Anonymous landing for the public view: every league, linking to its standings + fixtures.
@Component({
  selector: 'app-public-leagues-page',
  imports: [RouterLink, PublicHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex-1 bg-slate-50 dark:bg-slate-950">
      <app-public-header />
      <main class="mx-auto w-full max-w-3xl px-4 py-10">
        <h1 class="font-mono text-2xl font-semibold text-slate-900 dark:text-slate-100">Leagues</h1>
        <p class="mt-1 font-mono text-sm text-slate-500 dark:text-slate-400">
          Standings and fixtures, no sign-in needed.
        </p>

        <ul
          class="mt-6 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900"
        >
          @if (loading()) {
            <li class="px-4 py-3 font-mono text-sm text-slate-400 dark:text-slate-500">Loading…</li>
          } @else {
            @for (l of leagues(); track l.id) {
              <li class="px-4 py-3">
                <a
                  [routerLink]="['/public/leagues', l.id]"
                  class="font-mono text-sm font-medium text-slate-900 hover:underline dark:text-slate-100"
                  >{{ l.name }}</a
                >
                @if (l.description) {
                  <span class="ml-2 font-mono text-sm text-slate-500 dark:text-slate-400"
                    >— {{ l.description }}</span
                  >
                }
              </li>
            } @empty {
              <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                No leagues yet.
              </li>
            }
          }
        </ul>
      </main>
    </div>
  `,
})
export default class PublicLeaguesPage {
  private readonly api = inject(PublicApi);

  protected readonly leagues = signal<PublicLeague[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    this.api.listLeagues().subscribe({
      next: (rows) => {
        this.leagues.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
