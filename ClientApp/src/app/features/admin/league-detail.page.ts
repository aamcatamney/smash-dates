import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, switchMap, tap } from 'rxjs';
import {
  CreateDivisionRequest,
  DivisionGender,
  DivisionSummary,
  LeagueDetail,
  DivisionTable,
  LeaguesApi,
  MatchSummary,
  MembershipSummary,
  SeasonEntrySummary,
  SeasonSummary,
  WeekType,
} from './leagues.api';
import { ClubsApi, ClubSummary } from './clubs.api';

interface TeamOption {
  id: string;
  label: string;
}
import { AdminHeaderComponent } from './admin-header.component';
import { ModalComponent } from '../../shared/modal.component';

@Component({
  selector: 'app-league-detail-page',
  imports: [ReactiveFormsModule, RouterLink, AdminHeaderComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        @if (league(); as l) {
          <h1 class="font-mono text-2xl font-semibold text-slate-900">{{ l.name }}</h1>
          @if (l.description) {
            <p class="mt-1 font-mono text-sm text-slate-500">{{ l.description }}</p>
          }
          <a
            [routerLink]="['/admin/leagues', leagueId, 'admins']"
            class="mt-2 inline-block font-mono text-xs uppercase tracking-wider text-slate-500 hover:underline"
            >manage admins →</a
          >
        }

        <div class="mt-8 flex items-center justify-between">
          <h2 class="font-mono text-lg font-semibold text-slate-900">Divisions</h2>
          <button
            type="button"
            (click)="divisionDialogOpen.set(true)"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50"
          >
            ＋ Add division
          </button>
        </div>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (d of divisions(); track d.id) {
            <li class="px-4 py-3 font-mono text-sm text-slate-900">
              {{ d.name }} — {{ d.gender }} #{{ d.rank }} · rubbers/match {{ d.rubbersPerMatch }} ·
              points {{ d.winPoints }}/{{ d.drawPoints }}/{{ d.lossPoints }}
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No divisions yet.</li>
          }
        </ul>

        <app-modal [open]="divisionDialogOpen()" title="Add division" (closed)="divisionDialogOpen.set(false)">
        <form
          [formGroup]="form"
          (ngSubmit)="onCreate()"
          class="grid gap-3"
        >
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Name</span>
            <input
              type="text"
              formControlName="name"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              required
            />
          </label>
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Gender</span>
            <select
              formControlName="gender"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
            >
              <option value="Mens">Mens</option>
              <option value="Ladies">Ladies</option>
              <option value="Mixed">Mixed</option>
            </select>
          </label>
          <div class="grid grid-cols-2 gap-3">
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Rank</span>
              <input
                type="number"
                formControlName="rank"
                min="1"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Rubbers/match</span>
              <input
                type="number"
                formControlName="rubbersPerMatch"
                min="1"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
            </label>
          </div>
          <div class="grid grid-cols-3 gap-3">
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Win pts</span>
              <input
                type="number"
                formControlName="winPoints"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Draw pts</span>
              <input
                type="number"
                formControlName="drawPoints"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Loss pts</span>
              <input
                type="number"
                formControlName="lossPoints"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
            </label>
          </div>
          <button
            type="submit"
            [disabled]="submitting() || form.invalid"
            class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ submitting() ? 'Adding…' : 'Add division' }}
          </button>
          @if (error()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ error() }}</p>
          }
        </form>
        </app-modal>

        <div class="mt-10 flex items-center justify-between">
          <h2 class="font-mono text-lg font-semibold text-slate-900">Seasons</h2>
          <button
            type="button"
            (click)="seasonDialogOpen.set(true)"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50"
          >
            ＋ Add season
          </button>
        </div>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (s of seasons(); track s.id) {
            <li class="px-4 py-3 font-mono text-sm">
              <div class="flex items-center justify-between">
                <span>
                  {{ s.name }}
                  <span class="ml-2 text-slate-500">{{ s.startDate }} → {{ s.endDate }}</span>
                  <span class="ml-3 inline-block rounded bg-slate-200 px-2 py-0.5 text-xs">{{ s.status }}</span>
                </span>
                <div class="flex gap-2">
                  @if (s.status === 'Draft') {
                    <button
                      type="button"
                      (click)="onEditWeeks(s)"
                      class="rounded-md border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50"
                    >
                      {{ editingSeasonId() === s.id ? 'Close' : 'Weeks' }}
                    </button>
                    <button
                      type="button"
                      (click)="onManageEntries(s)"
                      class="rounded-md border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50"
                    >
                      {{ entriesSeasonId() === s.id ? 'Close' : 'Teams' }}
                    </button>
                    <button
                      type="button"
                      [disabled]="generatingSeasonId() === s.id"
                      (click)="onGenerate(s)"
                      class="rounded-md bg-slate-900 px-3 py-1 text-xs font-medium text-amber-300 hover:bg-slate-800 disabled:opacity-50"
                    >
                      {{ generatingSeasonId() === s.id ? 'Generating…' : 'Generate' }}
                    </button>
                    <button
                      type="button"
                      [attr.aria-label]="'Delete season ' + s.name"
                      (click)="onDeleteSeason(s)"
                      class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
                    >
                      Delete
                    </button>
                  } @else {
                    <button
                      type="button"
                      (click)="onToggleFixtures(s)"
                      class="rounded-md border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50"
                    >
                      {{ fixturesSeasonId() === s.id ? 'Close' : 'Fixtures' }}
                    </button>
                    @if (s.status === 'Proposed') {
                      <button
                        type="button"
                        [disabled]="rerunningSeasonId() === s.id"
                        (click)="onRerun(s)"
                        class="rounded-md border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                      >
                        {{ rerunningSeasonId() === s.id ? 'Re-running…' : 'Re-run' }}
                      </button>
                      <button
                        type="button"
                        (click)="onActivate(s)"
                        class="rounded-md bg-slate-900 px-3 py-1 text-xs font-medium text-amber-300 hover:bg-slate-800"
                      >Activate</button>
                    }
                    @if (s.status === 'Active') {
                      <button
                        type="button"
                        (click)="onCloseSeason(s)"
                        class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
                      >Close season</button>
                    }
                    <button
                      type="button"
                      (click)="onToggleStandings(s)"
                      class="rounded-md border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50"
                    >
                      {{ standingsSeasonId() === s.id ? 'Close' : 'Table' }}
                    </button>
                  }
                </div>
                @if (rerunError() && rerunErrorSeasonId() === s.id) {
                  <p class="mt-1 text-xs text-red-600" role="alert">{{ rerunError() }}</p>
                }
              </div>

              @if (editingSeasonId() === s.id) {
                <form [formGroup]="weeksForm" (ngSubmit)="onSaveWeeks(s)" class="mt-3 grid gap-2">
                  <div formArrayName="weeks" class="grid gap-2">
                    @for (row of weekRows.controls; track $index) {
                      <div [formGroupName]="$index" class="flex flex-wrap items-center gap-2">
                        <input
                          type="date"
                          formControlName="startDate"
                          aria-label="Week start date"
                          class="rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-2 focus:ring-slate-900"
                        />
                        <span class="text-slate-400">→</span>
                        <input
                          type="date"
                          formControlName="endDate"
                          aria-label="Week end date"
                          class="rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-2 focus:ring-slate-900"
                        />
                        <select
                          formControlName="weekType"
                          aria-label="Week type"
                          class="rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-2 focus:ring-slate-900"
                        >
                          <option value="Level">Level</option>
                          <option value="Mixed">Mixed</option>
                        </select>
                        <button
                          type="button"
                          (click)="removeWeek($index)"
                          aria-label="Remove week"
                          class="rounded-md border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50"
                        >✕</button>
                      </div>
                    } @empty {
                      <p class="text-xs text-slate-500">No weeks. Add one below.</p>
                    }
                  </div>
                  <div class="flex gap-2">
                    <button
                      type="button"
                      (click)="addWeek()"
                      class="rounded-md border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50"
                    >+ Add week</button>
                    <button
                      type="submit"
                      [disabled]="weeksSaving() || weeksForm.invalid"
                      class="rounded-md bg-slate-900 px-3 py-1 text-xs font-medium text-amber-300 disabled:opacity-50"
                    >
                      {{ weeksSaving() ? 'Saving…' : 'Save weeks' }}
                    </button>
                  </div>
                  @if (weeksError()) {
                    <p class="text-xs text-red-600" role="alert">{{ weeksError() }}</p>
                  }
                </form>
              }

              @if (entriesSeasonId() === s.id) {
                <div class="mt-3 grid gap-2">
                  <ul class="divide-y divide-slate-100 rounded border border-slate-200">
                    @for (e of seasonEntries(); track e.id) {
                      <li class="flex items-center justify-between px-3 py-2 text-xs">
                        <span>{{ e.teamName }} <span class="text-slate-400">→</span> {{ e.divisionName }}</span>
                        <button
                          type="button"
                          [attr.aria-label]="'Remove ' + e.teamName"
                          (click)="onRemoveEntry(s, e)"
                          class="rounded-md border border-red-300 px-2 py-0.5 text-red-700 hover:bg-red-50"
                        >✕</button>
                      </li>
                    } @empty {
                      <li class="px-3 py-2 text-xs text-slate-500">No teams assigned.</li>
                    }
                  </ul>
                  <form [formGroup]="entryForm" (ngSubmit)="onAddEntry(s)" class="flex flex-wrap items-center gap-2">
                    <select
                      formControlName="teamId"
                      aria-label="Team"
                      class="rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-2 focus:ring-slate-900"
                    >
                      <option value="">-- team --</option>
                      @for (t of teamOptions(); track t.id) {
                        <option [value]="t.id">{{ t.label }}</option>
                      }
                    </select>
                    <select
                      formControlName="divisionId"
                      aria-label="Division"
                      class="rounded-md border border-slate-300 px-2 py-1 text-xs focus:outline-none focus:ring-2 focus:ring-slate-900"
                    >
                      <option value="">-- division --</option>
                      @for (d of divisions(); track d.id) {
                        <option [value]="d.id">{{ d.name }} ({{ d.gender }})</option>
                      }
                    </select>
                    <button
                      type="submit"
                      [disabled]="entryForm.invalid"
                      class="rounded-md bg-slate-900 px-3 py-1 text-xs font-medium text-amber-300 disabled:opacity-50"
                    >Assign</button>
                  </form>
                  @if (entryError()) {
                    <p class="text-xs text-red-600" role="alert">{{ entryError() }}</p>
                  }
                </div>
              }

              @if (generateError() && generateErrorSeasonId() === s.id) {
                <p class="mt-2 text-xs text-red-600" role="alert">{{ generateError() }}</p>
              }

              @if (fixturesSeasonId() === s.id) {
                <ul class="mt-3 divide-y divide-slate-100 rounded border border-slate-200">
                  @for (f of fixtures(); track f.id) {
                    <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-3 py-2 text-xs">
                      <span class="font-semibold">{{ f.matchDate }}</span>
                      <span class="inline-block rounded bg-slate-200 px-1.5 py-0.5">{{ f.divisionName }}</span>
                      <span>
                        {{ f.homeTeamName }}
                        @if (f.status === 'Played') {
                          <span class="font-semibold">{{ f.homeScore }}–{{ f.awayScore }}</span>
                        } @else {
                          <span class="text-slate-400">v</span>
                        }
                        {{ f.awayTeamName }}
                      </span>
                      @if (f.isWalkover) { <span class="rounded bg-amber-200 px-1 text-amber-800">w/o</span> }
                      <span class="text-slate-500">@ {{ f.venueName }}</span>
                      @if (f.status === 'Proposed') {
                        <span class="text-slate-400">({{ f.homeAccepted ? 'home ✓' : 'home …' }}, {{ f.awayAccepted ? 'away ✓' : 'away …' }})</span>
                      }
                      <span class="ml-auto inline-block rounded bg-slate-100 px-1.5 py-0.5 text-slate-600">{{ f.status }}</span>
                      @if (f.status === 'Proposed') {
                        <button
                          type="button"
                          (click)="onForceConfirm(s, f)"
                          class="rounded-md border border-slate-300 px-2 py-0.5 text-slate-700 hover:bg-slate-50"
                        >Force confirm</button>
                      }
                      @if (f.status === 'Confirmed') {
                        <button type="button" (click)="onOpenResult(f)" class="rounded-md border border-slate-300 px-2 py-0.5 text-slate-700 hover:bg-slate-50">Result</button>
                        <button type="button" (click)="onWalkover(s, f, 'Home')" class="rounded-md border border-slate-300 px-2 py-0.5 text-slate-700 hover:bg-slate-50">W/O home</button>
                        <button type="button" (click)="onWalkover(s, f, 'Away')" class="rounded-md border border-slate-300 px-2 py-0.5 text-slate-700 hover:bg-slate-50">W/O away</button>
                        @if (s.status === 'Active') {
                          <button type="button" (click)="onPostpone(s, f)" class="rounded-md border border-amber-300 px-2 py-0.5 text-amber-700 hover:bg-amber-50">Postpone</button>
                        }
                      }

                      @if (resultMatchId() === f.id) {
                        <form [formGroup]="resultForm" (ngSubmit)="onSaveResult(s, f)" class="flex w-full items-center gap-2 pt-1">
                          <input type="number" formControlName="homeScore" min="0" aria-label="Home score" class="w-16 rounded-md border border-slate-300 px-2 py-1" />
                          <span class="text-slate-400">–</span>
                          <input type="number" formControlName="awayScore" min="0" aria-label="Away score" class="w-16 rounded-md border border-slate-300 px-2 py-1" />
                          <button type="submit" class="rounded-md bg-slate-900 px-2 py-1 font-medium text-amber-300">Save</button>
                          @if (resultError()) { <span class="text-red-600" role="alert">{{ resultError() }}</span> }
                        </form>
                      }
                    </li>
                  } @empty {
                    <li class="px-3 py-2 text-xs text-slate-500">No fixtures.</li>
                  }
                </ul>
              }

              @if (standingsSeasonId() === s.id) {
                @for (t of standings(); track t.divisionId) {
                  <div class="mt-3">
                    <h4 class="font-mono text-xs font-semibold text-slate-700">{{ t.divisionName }}</h4>
                    <table class="mt-1 w-full border border-slate-200 text-xs">
                      <thead class="bg-slate-100 text-slate-600">
                        <tr>
                          <th class="px-2 py-1 text-left">Team</th>
                          <th class="px-2 py-1">P</th><th class="px-2 py-1">W</th><th class="px-2 py-1">D</th><th class="px-2 py-1">L</th>
                          <th class="px-2 py-1">RF</th><th class="px-2 py-1">RA</th><th class="px-2 py-1">+/-</th><th class="px-2 py-1">Pts</th>
                        </tr>
                      </thead>
                      <tbody>
                        @for (r of t.rows; track r.teamId) {
                          <tr class="border-t border-slate-100">
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
                } @empty {
                  <p class="mt-3 text-xs text-slate-500">No standings yet.</p>
                }
              }
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No seasons yet.</li>
          }
        </ul>

        <app-modal [open]="seasonDialogOpen()" title="Add season" (closed)="seasonDialogOpen.set(false)">
        <form
          [formGroup]="seasonForm"
          (ngSubmit)="onCreateSeason()"
          class="grid gap-3"
        >
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Season name</span>
            <input
              type="text"
              formControlName="name"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              required
            />
          </label>
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Start</span>
            <input
              type="date"
              formControlName="startDate"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
            />
          </label>
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">End</span>
            <input
              type="date"
              formControlName="endDate"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
            />
          </label>
          <button
            type="submit"
            [disabled]="seasonSubmitting() || seasonForm.invalid"
            class="rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ seasonSubmitting() ? 'Adding…' : 'Add season' }}
          </button>
          @if (seasonError()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ seasonError() }}</p>
          }
        </form>
        </app-modal>

        <div class="mt-10 flex items-center justify-between">
          <h2 class="font-mono text-lg font-semibold text-slate-900">Member clubs</h2>
          <button
            type="button"
            (click)="inviteDialogOpen.set(true)"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50"
          >
            ＋ Invite club
          </button>
        </div>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (m of memberships(); track m.id) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                club <span class="text-slate-500">{{ m.clubId }}</span>
                <span class="ml-3 inline-block rounded bg-slate-200 px-2 py-0.5 text-xs">{{ m.status }}</span>
              </span>
              @if (m.status === 'Accepted') {
                <button
                  type="button"
                  (click)="onExpel(m)"
                  class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
                >Expel</button>
              }
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No member clubs.</li>
          }
        </ul>

        <app-modal [open]="inviteDialogOpen()" title="Invite club" (closed)="inviteDialogOpen.set(false)">
        <form
          [formGroup]="inviteForm"
          (ngSubmit)="onInvite()"
          class="grid gap-3"
        >
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Invite club</span>
            <select
              formControlName="clubId"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
            >
              <option value="">-- choose a club --</option>
              @for (c of availableClubs(); track c.id) {
                <option [value]="c.id">{{ c.shortCode }} · {{ c.name }}</option>
              }
            </select>
          </label>
          <button
            type="submit"
            [disabled]="inviteForm.invalid"
            class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            Send invite
          </button>
        </form>
        </app-modal>
      </main>
    </div>
  `,
})
export default class LeagueDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(LeaguesApi);
  private readonly clubsApi = inject(ClubsApi);

  protected readonly league = signal<LeagueDetail | null>(null);
  protected readonly divisions = signal<DivisionSummary[]>([]);
  protected readonly memberships = signal<MembershipSummary[]>([]);
  protected readonly availableClubs = signal<ClubSummary[]>([]);
  protected readonly seasons = signal<SeasonSummary[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly divisionDialogOpen = signal(false);
  protected readonly seasonDialogOpen = signal(false);
  protected readonly inviteDialogOpen = signal(false);
  protected readonly seasonError = signal<string | null>(null);
  protected readonly seasonSubmitting = signal(false);
  protected readonly editingSeasonId = signal<string | null>(null);
  protected readonly weeksError = signal<string | null>(null);
  protected readonly weeksSaving = signal(false);
  protected readonly entriesSeasonId = signal<string | null>(null);
  protected readonly seasonEntries = signal<SeasonEntrySummary[]>([]);
  protected readonly teamOptions = signal<TeamOption[]>([]);
  protected readonly entryError = signal<string | null>(null);
  protected readonly generatingSeasonId = signal<string | null>(null);
  protected readonly generateError = signal<string | null>(null);
  protected readonly generateErrorSeasonId = signal<string | null>(null);
  protected readonly fixturesSeasonId = signal<string | null>(null);
  protected readonly fixtures = signal<MatchSummary[]>([]);
  protected readonly rerunningSeasonId = signal<string | null>(null);
  protected readonly rerunError = signal<string | null>(null);
  protected readonly rerunErrorSeasonId = signal<string | null>(null);
  protected readonly standingsSeasonId = signal<string | null>(null);
  protected readonly standings = signal<DivisionTable[]>([]);
  protected readonly resultMatchId = signal<string | null>(null);
  protected readonly resultError = signal<string | null>(null);
  protected leagueId = '';

  protected readonly resultForm = new FormGroup({
    homeScore: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    awayScore: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
  });

  protected readonly entryForm = new FormGroup({
    teamId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    divisionId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly seasonForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    startDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    endDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly weeksForm = new FormGroup({
    weeks: new FormArray<ReturnType<typeof LeagueDetailPage.makeWeekRow>>([]),
  });

  protected get weekRows(): FormArray {
    return this.weeksForm.get('weeks') as FormArray;
  }

  private static makeWeekRow(startDate = '', endDate = '', weekType: WeekType = 'Level') {
    return new FormGroup({
      startDate: new FormControl(startDate, { nonNullable: true, validators: [Validators.required] }),
      endDate: new FormControl(endDate, { nonNullable: true, validators: [Validators.required] }),
      weekType: new FormControl<WeekType>(weekType, { nonNullable: true, validators: [Validators.required] }),
    });
  }

  protected readonly inviteForm = new FormGroup({
    clubId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    gender: new FormControl<DivisionGender>('Mens', { nonNullable: true }),
    rank: new FormControl(1, { nonNullable: true, validators: [Validators.min(1)] }),
    rubbersPerMatch: new FormControl(9, { nonNullable: true, validators: [Validators.min(1)] }),
    winPoints: new FormControl(2, { nonNullable: true, validators: [Validators.min(0)] }),
    drawPoints: new FormControl(1, { nonNullable: true, validators: [Validators.min(0)] }),
    lossPoints: new FormControl(0, { nonNullable: true, validators: [Validators.min(0)] }),
  });

  constructor() {
    this.route.paramMap
      .pipe(
        tap((p) => {
          this.leagueId = p.get('id') ?? '';
        }),
        switchMap((p) => this.api.get(p.get('id') ?? '')),
        tap((l) => this.league.set(l)),
        tap(() => {
          this.refreshMemberships();
          this.refreshAvailableClubs();
          this.refreshSeasons();
        }),
        switchMap((l) => this.api.listDivisions(l.id)),
      )
      .subscribe({
        next: (rows) => this.divisions.set(rows),
        error: () => this.error.set('Failed to load league.'),
      });
  }

  private refreshMemberships(): void {
    if (!this.leagueId) return;
    this.api.listMemberships(this.leagueId).subscribe({
      next: (rows) => this.memberships.set(rows),
    });
  }

  private refreshAvailableClubs(): void {
    this.clubsApi.list().subscribe({
      next: (rows) => this.availableClubs.set(rows),
    });
  }

  protected onInvite(): void {
    const clubId = this.inviteForm.getRawValue().clubId;
    if (!clubId) return;
    this.api.invite(this.leagueId, clubId).subscribe({
      next: () => {
        this.inviteForm.reset({ clubId: '' });
        this.inviteDialogOpen.set(false);
        this.refreshMemberships();
      },
      error: (err: { error?: { title?: string } }) =>
        this.error.set(err?.error?.title ?? 'Invite failed.'),
    });
  }

  protected onExpel(m: MembershipSummary): void {
    this.api.expel(this.leagueId, m.id).subscribe({
      next: () => this.refreshMemberships(),
    });
  }

  protected onCreate(): void {
    if (!this.leagueId) return;
    const value = this.form.getRawValue();
    const request: CreateDivisionRequest = {
      name: value.name.trim(),
      gender: value.gender,
      rank: value.rank,
      rubbersPerMatch: value.rubbersPerMatch,
      winPoints: value.winPoints,
      drawPoints: value.drawPoints,
      lossPoints: value.lossPoints,
    };
    if (!request.name) return;

    this.submitting.set(true);
    this.error.set(null);
    this.api.createDivision(this.leagueId, request).subscribe({
      next: () => {
        this.submitting.set(false);
        this.form.reset({
          name: '',
          gender: 'Mens',
          rank: 1,
          rubbersPerMatch: 9,
          winPoints: 2,
          drawPoints: 1,
          lossPoints: 0,
        });
        this.divisionDialogOpen.set(false);
        this.api.listDivisions(this.leagueId).subscribe({
          next: (rows) => this.divisions.set(rows),
        });
      },
      error: (err: { error?: { title?: string } }) => {
        this.submitting.set(false);
        this.error.set(err?.error?.title ?? 'Create failed.');
      },
    });
  }

  private refreshSeasons(): void {
    if (!this.leagueId) return;
    this.api.listSeasons(this.leagueId).subscribe({
      next: (rows) => this.seasons.set(rows),
    });
  }

  protected onCreateSeason(): void {
    if (!this.leagueId) return;
    const value = this.seasonForm.getRawValue();
    const name = value.name.trim();
    if (!name) return;

    this.seasonSubmitting.set(true);
    this.seasonError.set(null);
    this.api
      .createSeason(this.leagueId, {
        name,
        startDate: value.startDate,
        endDate: value.endDate,
        weeks: [],
      })
      .subscribe({
        next: () => {
          this.seasonSubmitting.set(false);
          this.seasonForm.reset({ name: '', startDate: '', endDate: '' });
          this.seasonDialogOpen.set(false);
          this.refreshSeasons();
        },
        error: (err: { error?: { title?: string } }) => {
          this.seasonSubmitting.set(false);
          this.seasonError.set(err?.error?.title ?? 'Could not create season.');
        },
      });
  }

  protected onDeleteSeason(s: SeasonSummary): void {
    this.api.deleteSeason(this.leagueId, s.id).subscribe({
      next: () => {
        if (this.editingSeasonId() === s.id) this.editingSeasonId.set(null);
        this.refreshSeasons();
      },
    });
  }

  protected onEditWeeks(s: SeasonSummary): void {
    if (this.editingSeasonId() === s.id) {
      this.editingSeasonId.set(null);
      return;
    }
    this.weeksError.set(null);
    this.weekRows.clear();
    this.api.getSeason(this.leagueId, s.id).subscribe({
      next: (detail) => {
        for (const w of detail.weeks) {
          this.weekRows.push(LeagueDetailPage.makeWeekRow(w.startDate, w.endDate, w.weekType));
        }
        this.editingSeasonId.set(s.id);
      },
    });
  }

  protected addWeek(): void {
    this.weekRows.push(LeagueDetailPage.makeWeekRow());
  }

  protected removeWeek(index: number): void {
    this.weekRows.removeAt(index);
  }

  protected onSaveWeeks(s: SeasonSummary): void {
    this.weeksSaving.set(true);
    this.weeksError.set(null);
    const weeks = this.weeksForm.getRawValue().weeks;
    this.api.replaceSeasonWeeks(this.leagueId, s.id, weeks).subscribe({
      next: () => {
        this.weeksSaving.set(false);
        this.editingSeasonId.set(null);
        this.refreshSeasons();
      },
      error: (err: { error?: { title?: string } }) => {
        this.weeksSaving.set(false);
        this.weeksError.set(err?.error?.title ?? 'Could not save weeks.');
      },
    });
  }

  protected onManageEntries(s: SeasonSummary): void {
    if (this.entriesSeasonId() === s.id) {
      this.entriesSeasonId.set(null);
      return;
    }
    this.entryError.set(null);
    this.entryForm.reset({ teamId: '', divisionId: '' });
    this.loadTeamOptions();
    this.refreshEntries(s.id);
    this.entriesSeasonId.set(s.id);
  }

  private refreshEntries(seasonId: string): void {
    this.api.listSeasonEntries(this.leagueId, seasonId).subscribe({
      next: (rows) => this.seasonEntries.set(rows),
    });
  }

  // Teams eligible for entry come from clubs with an Accepted membership in this league.
  private loadTeamOptions(): void {
    const acceptedClubIds = this.memberships()
      .filter((m) => m.status === 'Accepted')
      .map((m) => m.clubId);
    const clubsById = new Map(this.availableClubs().map((c) => [c.id, c]));

    if (acceptedClubIds.length === 0) {
      this.teamOptions.set([]);
      return;
    }

    forkJoin(acceptedClubIds.map((clubId) => this.clubsApi.listTeams(clubId))).subscribe({
      next: (teamsPerClub) => {
        const options: TeamOption[] = [];
        teamsPerClub.forEach((teams, i) => {
          const code = clubsById.get(acceptedClubIds[i])?.shortCode ?? '???';
          for (const t of teams) {
            options.push({ id: t.id, label: `${code} · ${t.name} (${t.gender})` });
          }
        });
        this.teamOptions.set(options);
      },
    });
  }

  protected onAddEntry(s: SeasonSummary): void {
    const { teamId, divisionId } = this.entryForm.getRawValue();
    if (!teamId || !divisionId) return;
    this.entryError.set(null);
    this.api.createSeasonEntry(this.leagueId, s.id, teamId, divisionId).subscribe({
      next: () => {
        this.entryForm.reset({ teamId: '', divisionId: '' });
        this.refreshEntries(s.id);
      },
      error: (err: { error?: { title?: string } }) =>
        this.entryError.set(err?.error?.title ?? 'Could not assign team.'),
    });
  }

  protected onRemoveEntry(s: SeasonSummary, e: SeasonEntrySummary): void {
    this.api.deleteSeasonEntry(this.leagueId, s.id, e.id).subscribe({
      next: () => this.refreshEntries(s.id),
    });
  }

  protected onGenerate(s: SeasonSummary): void {
    this.generatingSeasonId.set(s.id);
    this.generateError.set(null);
    this.generateErrorSeasonId.set(null);
    this.api.generateSchedule(this.leagueId, s.id).subscribe({
      next: () => {
        this.generatingSeasonId.set(null);
        this.refreshSeasons();
        this.openFixtures(s.id);
      },
      error: (err: { error?: { title?: string } }) => {
        this.generatingSeasonId.set(null);
        this.generateErrorSeasonId.set(s.id);
        this.generateError.set(err?.error?.title ?? 'Could not generate a schedule.');
      },
    });
  }

  protected onToggleFixtures(s: SeasonSummary): void {
    if (this.fixturesSeasonId() === s.id) {
      this.fixturesSeasonId.set(null);
      return;
    }
    this.openFixtures(s.id);
  }

  private openFixtures(seasonId: string): void {
    this.api.listMatches(this.leagueId, seasonId).subscribe({
      next: (rows) => {
        this.fixtures.set(rows);
        this.fixturesSeasonId.set(seasonId);
      },
    });
  }

  protected onForceConfirm(s: SeasonSummary, f: MatchSummary): void {
    this.api.forceConfirmMatch(f.id).subscribe({
      next: () => this.openFixtures(s.id),
    });
  }

  protected onToggleStandings(s: SeasonSummary): void {
    if (this.standingsSeasonId() === s.id) {
      this.standingsSeasonId.set(null);
      return;
    }
    this.api.listStandings(this.leagueId, s.id).subscribe({
      next: (tables) => {
        this.standings.set(tables);
        this.standingsSeasonId.set(s.id);
      },
    });
  }

  protected onOpenResult(f: MatchSummary): void {
    this.resultError.set(null);
    this.resultForm.reset({ homeScore: 0, awayScore: 0 });
    this.resultMatchId.set(this.resultMatchId() === f.id ? null : f.id);
  }

  protected onSaveResult(s: SeasonSummary, f: MatchSummary): void {
    const { homeScore, awayScore } = this.resultForm.getRawValue();
    this.resultError.set(null);
    this.api.recordResult(f.id, Number(homeScore), Number(awayScore), f.matchDate).subscribe({
      next: () => {
        this.resultMatchId.set(null);
        this.openFixtures(s.id);
      },
      error: (err: { error?: { title?: string } }) =>
        this.resultError.set(err?.error?.title ?? 'Could not record result.'),
    });
  }

  protected onWalkover(s: SeasonSummary, f: MatchSummary, winner: 'Home' | 'Away'): void {
    this.api.recordWalkover(f.id, winner).subscribe({
      next: () => this.openFixtures(s.id),
    });
  }

  protected onPostpone(s: SeasonSummary, f: MatchSummary): void {
    this.api.postponeMatch(f.id).subscribe({
      next: () => this.openFixtures(s.id),
    });
  }

  protected onActivate(s: SeasonSummary): void {
    this.api.activateSeason(this.leagueId, s.id).subscribe({ next: () => this.refreshSeasons() });
  }

  protected onCloseSeason(s: SeasonSummary): void {
    this.api.closeSeason(this.leagueId, s.id).subscribe({ next: () => this.refreshSeasons() });
  }

  protected onRerun(s: SeasonSummary): void {
    this.rerunningSeasonId.set(s.id);
    this.rerunError.set(null);
    this.rerunErrorSeasonId.set(null);
    this.api.rerunSchedule(this.leagueId, s.id).subscribe({
      next: () => {
        this.rerunningSeasonId.set(null);
        this.openFixtures(s.id);
      },
      error: (err: { error?: { title?: string } }) => {
        this.rerunningSeasonId.set(null);
        this.rerunErrorSeasonId.set(s.id);
        this.rerunError.set(err?.error?.title ?? 'Could not re-run the schedule.');
      },
    });
  }
}
