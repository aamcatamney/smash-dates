import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  PublicApi,
  PublicFixture,
  PublicLeagueDetail,
  PublicSeason,
  PublicStandingsTable,
} from './public.api';
import { PublicHeaderComponent } from './public-header.component';
import { StatusColorPipe } from '../../shared/status-color.pipe';

// Anonymous league page: pick a season, see its division standings and full fixture list.
@Component({
  selector: 'app-public-league-page',
  imports: [RouterLink, PublicHeaderComponent, StatusColorPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex-1 bg-slate-50 dark:bg-slate-950">
      <app-public-header />
      <main class="mx-auto w-full max-w-4xl px-4 py-10">
        <a
          routerLink="/public"
          class="font-mono text-xs uppercase tracking-wider text-slate-500 hover:underline dark:text-slate-400"
          >← all leagues</a
        >

        @if (loading()) {
          <p class="mt-10 text-center font-mono text-sm text-slate-500 dark:text-slate-400">
            Loading…
          </p>
        } @else if (league(); as lg) {
          <h1 class="mt-2 font-mono text-2xl font-semibold text-slate-900 dark:text-slate-100">
            {{ lg.name }}
          </h1>
          @if (lg.description) {
            <p class="mt-1 font-mono text-sm text-slate-500 dark:text-slate-400">
              {{ lg.description }}
            </p>
          }

          @if (lg.seasons.length === 0) {
            <p class="mt-8 font-mono text-sm text-slate-500 dark:text-slate-400">
              No published seasons yet.
            </p>
          } @else {
            <label class="mt-6 grid max-w-xs gap-1">
              <span
                class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                >Season</span
              >
              <select
                [value]="selectedSeasonId()"
                (change)="onSeasonChange($event)"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
              >
                @for (s of lg.seasons; track s.id) {
                  <option [value]="s.id">{{ s.name }} ({{ s.status }})</option>
                }
              </select>
            </label>

            <!-- Standings -->
            <h2 class="mt-8 font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
              Standings
            </h2>
            @for (t of standings(); track t.divisionId) {
              <div class="mt-3">
                <h3 class="font-mono text-xs font-semibold text-slate-700 dark:text-slate-300">
                  {{ t.divisionName }}
                </h3>
                <div class="mt-1 overflow-x-auto">
                  <table
                    class="w-full min-w-[32rem] border border-slate-200 text-xs dark:border-slate-800"
                  >
                    <thead
                      class="bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-400"
                    >
                      <tr>
                        <th class="px-2 py-1 text-left">Team</th>
                        <th class="px-2 py-1">P</th>
                        <th class="px-2 py-1">W</th>
                        <th class="px-2 py-1">D</th>
                        <th class="px-2 py-1">L</th>
                        <th class="px-2 py-1">RF</th>
                        <th class="px-2 py-1">RA</th>
                        <th class="px-2 py-1">+/-</th>
                        <th class="px-2 py-1">Pts</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (r of t.rows; track r.teamId) {
                        <tr class="border-t border-slate-100 dark:border-slate-800">
                          <td class="px-2 py-1 text-left">{{ r.teamName }}</td>
                          <td class="px-2 py-1 text-center">{{ r.played }}</td>
                          <td class="px-2 py-1 text-center">{{ r.won }}</td>
                          <td class="px-2 py-1 text-center">{{ r.drawn }}</td>
                          <td class="px-2 py-1 text-center">{{ r.lost }}</td>
                          <td class="px-2 py-1 text-center">{{ r.rubbersFor }}</td>
                          <td class="px-2 py-1 text-center">{{ r.rubbersAgainst }}</td>
                          <td class="px-2 py-1 text-center">{{ r.rubberDifference }}</td>
                          <td class="px-2 py-1 text-center font-semibold">{{ r.points }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            } @empty {
              <p class="mt-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                No standings yet.
              </p>
            }

            <!-- Fixtures -->
            <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
              Fixtures
            </h2>
            <ul
              class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900"
            >
              @for (f of fixtures(); track f.id) {
                <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 font-mono text-xs">
                  <span class="text-slate-500 dark:text-slate-400">{{ f.matchDate }}</span>
                  <span class="rounded bg-slate-200 px-1.5 py-0.5 dark:bg-slate-700">{{
                    f.divisionName
                  }}</span>
                  <span class="text-slate-900 dark:text-slate-100"
                    >{{ f.homeTeamName }} v {{ f.awayTeamName }}</span
                  >
                  @if (f.homeScore !== null && f.awayScore !== null) {
                    <span class="font-semibold text-slate-900 dark:text-slate-100"
                      >{{ f.homeScore }}–{{ f.awayScore }}</span
                    >
                    @if (f.isWalkover) {
                      <span class="text-slate-400">(walkover)</span>
                    }
                  }
                  <span class="text-slate-500 dark:text-slate-400">@ {{ f.venueName }}</span>
                  <span
                    [class]="'ml-auto inline-block rounded px-2 py-0.5 ' + (f.status | statusColor)"
                    >{{ f.status }}</span
                  >
                </li>
              } @empty {
                <li class="px-3 py-2 font-mono text-sm text-slate-500 dark:text-slate-400">
                  No fixtures yet.
                </li>
              }
            </ul>
          }
        } @else {
          <p class="mt-10 text-center font-mono text-sm text-slate-500 dark:text-slate-400">
            League not found.
          </p>
        }
      </main>
    </div>
  `,
})
export default class PublicLeaguePage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(PublicApi);

  protected readonly league = signal<PublicLeagueDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly selectedSeasonId = signal('');
  protected readonly standings = signal<PublicStandingsTable[]>([]);
  protected readonly fixtures = signal<PublicFixture[]>([]);

  private leagueId = '';

  constructor() {
    this.leagueId = this.route.snapshot.paramMap.get('leagueId') ?? '';
    this.api.getLeague(this.leagueId).subscribe({
      next: (lg) => {
        this.league.set(lg);
        this.loading.set(false);
        const initial = this.preferredSeason(lg.seasons);
        if (initial) this.selectSeason(initial.id);
      },
      error: () => this.loading.set(false),
    });
  }

  // Default to an Active season, else the first listed.
  private preferredSeason(seasons: PublicSeason[]): PublicSeason | undefined {
    return seasons.find((s) => s.status === 'Active') ?? seasons[0];
  }

  protected onSeasonChange(event: Event): void {
    this.selectSeason((event.target as HTMLSelectElement).value);
  }

  private selectSeason(seasonId: string): void {
    this.selectedSeasonId.set(seasonId);
    this.api
      .getStandings(this.leagueId, seasonId)
      .subscribe({ next: (t) => this.standings.set(t) });
    this.api.getFixtures(this.leagueId, seasonId).subscribe({ next: (f) => this.fixtures.set(f) });
  }
}
