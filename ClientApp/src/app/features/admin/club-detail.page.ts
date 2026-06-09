import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, switchMap, tap } from 'rxjs';
import {
  BlockedDateScope,
  BlockedDateSummary,
  ClubAdminSummary,
  ClubDetail,
  ClubMatch,
  ClubsApi,
  Gender,
  MembershipSummary,
  TeamSummary,
  VenueSummary,
} from './clubs.api';
import { LeaguesApi, LeagueSummary } from './leagues.api';
import { AdminHeaderComponent } from './admin-header.component';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmComponent } from '../../shared/confirm.component';
import { StatusColorPipe } from '../../shared/status-color.pipe';
import { CsvImportComponent } from '../../shared/csv-import.component';
import { ImportResult } from '../../shared/import-result';
import { ClubPlayersComponent } from './club-players.component';
import { PegboardSessionsComponent } from './pegboard-sessions.component';
import { TeamSquadComponent } from './team-squad.component';
import { TabsComponent, TabDef } from '../../shared/tabs.component';
import { CalendarSubscribeComponent } from '../../shared/calendar-subscribe.component';
import { ToastService } from '../../shared/toast.service';
import { PlayersApi } from './players.api';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-club-detail-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AdminHeaderComponent,
    ModalComponent,
    ConfirmComponent,
    StatusColorPipe,
    CsvImportComponent,
    ClubPlayersComponent,
    PegboardSessionsComponent,
    TeamSquadComponent,
    TabsComponent,
    CalendarSubscribeComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex-1 bg-slate-50 dark:bg-slate-950">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <a
          [routerLink]="['/clubs']"
          class="font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400 hover:underline"
          >← back to clubs</a
        >

        @if (loading()) {
          <p class="py-10 text-center font-mono text-sm text-slate-500 dark:text-slate-400">
            Loading…
          </p>
        } @else if (club()) {
          @if (club(); as c) {
            <h1 class="mt-2 font-mono text-2xl font-semibold text-slate-900 dark:text-slate-100">
              {{ c.shortCode }} · {{ c.name }}
            </h1>
            <p class="mt-1 font-mono text-sm text-slate-500 dark:text-slate-400">
              {{ c.contactEmail }}
            </p>
            @if (c.notes) {
              <p class="mt-1 font-mono text-sm text-slate-500 dark:text-slate-400">{{ c.notes }}</p>
            }
            <div class="mt-2">
              <app-calendar-subscribe
                [endpoint]="'/api/calendar/club/' + clubId() + '/url'"
                label="Subscribe to fixtures (iCal)"
              />
            </div>
          }

          <app-tabs #tabs [tabs]="clubTabs()" />

          @if (tabs.active() === 'admins') {
            <section role="tabpanel" id="panel-admins" aria-labelledby="tab-admins">
              <div class="mt-8 flex items-center justify-between">
                <h2 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
                  Club admins
                </h2>
                @if (canManage()) {
                  <button
                    type="button"
                    (click)="adminDialogOpen.set(true)"
                    class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-1 font-mono text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                  >
                    ＋ Add admin
                  </button>
                }
              </div>
              <ul
                class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900"
              >
                @for (admin of admins(); track admin.userId) {
                  <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
                    <span>
                      {{ admin.displayName ?? admin.email }}
                      <span class="ml-2 text-slate-500 dark:text-slate-400">{{ admin.email }}</span>
                    </span>
                    @if (canManage()) {
                      <button
                        type="button"
                        [attr.aria-label]="'Revoke ' + admin.email"
                        (click)="askRevoke(admin)"
                        class="rounded-md border border-red-300 dark:border-red-800 px-3 py-1 text-xs text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950"
                      >
                        Revoke
                      </button>
                    }
                  </li>
                } @empty {
                  <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                    No admins.
                  </li>
                }
              </ul>

              <app-modal
                [open]="adminDialogOpen()"
                title="Add club admin"
                (closed)="adminDialogOpen.set(false)"
              >
                <form [formGroup]="adminForm" (ngSubmit)="onGrant()" class="grid gap-3">
                  <label class="grid gap-1">
                    <span
                      class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                      >Add admin by email</span
                    >
                    <input
                      type="email"
                      formControlName="email"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                      required
                    />
                  </label>
                  <button
                    type="submit"
                    [disabled]="adminBusy() || adminForm.invalid"
                    class="justify-self-start rounded-md bg-slate-900 dark:bg-amber-400 px-4 py-2 font-mono text-sm font-medium text-amber-300 dark:text-slate-900 disabled:opacity-50"
                  >
                    {{ adminBusy() ? 'Granting…' : 'Grant admin' }}
                  </button>
                  @if (adminError()) {
                    <p class="font-mono text-sm text-red-600 dark:text-red-400" role="alert">
                      {{ adminError() }}
                    </p>
                  }
                </form>
              </app-modal>

              <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
                League memberships
              </h2>
              <ul
                class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900"
              >
                @for (m of memberships(); track m.id) {
                  <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
                    <span>
                      {{ leagueName(m.leagueId) }}
                      <span
                        [class]="
                          'ml-3 inline-block rounded px-2 py-0.5 text-xs ' +
                          (m.status | statusColor)
                        "
                        >{{ m.status }}</span
                      >
                    </span>
                    @if (canManage()) {
                      <div class="flex gap-2">
                        @if (m.status === 'Pending') {
                          <button
                            type="button"
                            (click)="onAccept(m)"
                            [disabled]="isBusy(m.id)"
                            class="rounded-md border border-emerald-300 dark:border-emerald-800 px-3 py-1 text-xs text-emerald-700 dark:text-emerald-400 hover:bg-emerald-50 dark:hover:bg-emerald-950 disabled:opacity-50"
                          >
                            Accept
                          </button>
                          <button
                            type="button"
                            (click)="onDecline(m)"
                            [disabled]="isBusy(m.id)"
                            class="rounded-md border border-amber-300 dark:border-amber-800 px-3 py-1 text-xs text-amber-700 dark:text-amber-400 hover:bg-amber-50 dark:hover:bg-amber-950 disabled:opacity-50"
                          >
                            Decline
                          </button>
                        }
                        @if (m.status === 'Accepted') {
                          <button
                            type="button"
                            (click)="onWithdraw(m)"
                            [disabled]="isBusy(m.id)"
                            class="rounded-md border border-red-300 dark:border-red-800 px-3 py-1 text-xs text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950 disabled:opacity-50"
                          >
                            Withdraw
                          </button>
                        }
                      </div>
                    }
                  </li>
                } @empty {
                  <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                    No memberships.
                  </li>
                }
              </ul>
            </section>
          }

          @if (tabs.active() === 'teams') {
            <section role="tabpanel" id="panel-teams" aria-labelledby="tab-teams">
              <div class="mt-10 flex items-center justify-between">
                <h2 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
                  Teams
                </h2>
                @if (canManage()) {
                  <div class="flex gap-2">
                    <button
                      type="button"
                      (click)="openImport('teams')"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-1 font-mono text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                    >
                      Import CSV
                    </button>
                    <button
                      type="button"
                      (click)="teamDialogOpen.set(true)"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-1 font-mono text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                    >
                      ＋ Add team
                    </button>
                  </div>
                }
              </div>
              <ul
                class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900"
              >
                @for (t of teams(); track t.id) {
                  <li class="px-4 py-3 font-mono text-sm">
                    <div class="flex items-center justify-between">
                      <span>
                        {{ t.name }}
                        <span
                          class="ml-2 inline-block rounded bg-slate-200 dark:bg-slate-700 px-2 py-0.5 text-xs uppercase"
                          >{{ t.gender }}</span
                        >
                      </span>
                      <div class="flex gap-2">
                        <button
                          type="button"
                          (click)="toggleSquad(t.id)"
                          class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-1 text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                        >
                          {{ squadTeamId() === t.id ? 'Close' : 'Squad' }}
                        </button>
                        @if (canManage()) {
                          <button
                            type="button"
                            [attr.aria-label]="'Delete team ' + t.name"
                            (click)="askDeleteTeam(t)"
                            class="rounded-md border border-red-300 dark:border-red-800 px-3 py-1 text-xs text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950"
                          >
                            Delete
                          </button>
                        }
                      </div>
                    </div>
                    <div class="mt-2">
                      <app-calendar-subscribe
                        [endpoint]="'/api/calendar/team/' + t.id + '/url'"
                        label="iCal"
                      />
                    </div>
                    @if (squadTeamId() === t.id) {
                      <app-team-squad [clubId]="clubId()" [teamId]="t.id" />
                    }
                  </li>
                } @empty {
                  <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                    No teams.
                  </li>
                }
              </ul>

              <app-modal
                [open]="teamDialogOpen()"
                title="Add team"
                (closed)="teamDialogOpen.set(false)"
              >
                <form [formGroup]="teamForm" (ngSubmit)="onCreateTeam()" class="grid gap-3">
                  <label class="grid gap-1">
                    <span
                      class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                      >Team name</span
                    >
                    <input
                      type="text"
                      formControlName="name"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                      required
                    />
                  </label>
                  <label class="grid gap-1">
                    <span
                      class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                      >Gender</span
                    >
                    <select
                      formControlName="gender"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                    >
                      <option value="Mens">Mens</option>
                      <option value="Ladies">Ladies</option>
                      <option value="Mixed">Mixed</option>
                    </select>
                  </label>
                  <button
                    type="submit"
                    [disabled]="teamBusy() || teamForm.invalid"
                    class="rounded-md bg-slate-900 dark:bg-amber-400 px-4 py-2 font-mono text-sm font-medium text-amber-300 dark:text-slate-900 disabled:opacity-50"
                  >
                    {{ teamBusy() ? 'Adding…' : 'Add team' }}
                  </button>
                  @if (teamError()) {
                    <p class="font-mono text-sm text-red-600 dark:text-red-400" role="alert">
                      {{ teamError() }}
                    </p>
                  }
                </form>
              </app-modal>
            </section>
          }

          @if (tabs.active() === 'venues') {
            <section role="tabpanel" id="panel-venues" aria-labelledby="tab-venues">
              <div class="mt-10 flex items-center justify-between">
                <h2 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
                  Venues
                </h2>
                @if (canManage()) {
                  <div class="flex gap-2">
                    <button
                      type="button"
                      (click)="openImport('venues')"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-1 font-mono text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                    >
                      Import CSV
                    </button>
                    <button
                      type="button"
                      (click)="venueDialogOpen.set(true)"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-1 font-mono text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                    >
                      ＋ Add venue
                    </button>
                  </div>
                }
              </div>
              <ul
                class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900"
              >
                @for (v of venues(); track v.id) {
                  <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
                    <span class="flex flex-col gap-0.5">
                      <span>
                        {{ v.name }}
                        <span
                          class="ml-2 inline-block rounded bg-slate-200 dark:bg-slate-700 px-2 py-0.5 text-xs"
                        >
                          {{ v.courts }} {{ v.courts === 1 ? 'court' : 'courts' }} · up to
                          {{ v.maxConcurrentMatches }}
                          {{ v.maxConcurrentMatches === 1 ? 'match' : 'matches' }} at once
                        </span>
                      </span>
                      @if (v.address) {
                        <span class="text-xs text-slate-500 dark:text-slate-400">
                          {{ v.address }} ·
                          <a
                            [href]="mapUrl(v.address)"
                            target="_blank"
                            rel="noopener noreferrer"
                            class="underline hover:text-slate-900 dark:hover:text-slate-100"
                            >Map ↗</a
                          >
                        </span>
                      }
                    </span>
                    @if (canManage()) {
                      <span class="flex gap-2">
                        <button
                          type="button"
                          [attr.aria-label]="'Edit venue ' + v.name"
                          (click)="openEditVenue(v)"
                          class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-1 text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                        >
                          Edit
                        </button>
                        <button
                          type="button"
                          [attr.aria-label]="'Delete venue ' + v.name"
                          (click)="askDeleteVenue(v)"
                          class="rounded-md border border-red-300 dark:border-red-800 px-3 py-1 text-xs text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950"
                        >
                          Delete
                        </button>
                      </span>
                    }
                  </li>
                } @empty {
                  <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                    No venues.
                  </li>
                }
              </ul>

              <app-modal
                [open]="venueDialogOpen()"
                [title]="editingVenueId() ? 'Edit venue' : 'Add venue'"
                (closed)="closeVenueDialog()"
              >
                <form [formGroup]="venueForm" (ngSubmit)="onSaveVenue()" class="grid gap-3">
                  <label class="grid gap-1">
                    <span
                      class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                      >Venue name</span
                    >
                    <input
                      type="text"
                      formControlName="name"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                      required
                    />
                  </label>
                  <label class="grid gap-1">
                    <span
                      class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                      >Courts</span
                    >
                    <input
                      type="number"
                      formControlName="courts"
                      min="1"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                    />
                  </label>
                  <label class="grid gap-1">
                    <span
                      class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                      >Max concurrent matches</span
                    >
                    <select
                      formControlName="maxConcurrentMatches"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                    >
                      <option [value]="1">1</option>
                      <option [value]="2">2</option>
                    </select>
                  </label>
                  <label class="grid gap-1">
                    <span
                      class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                      >Address (optional)</span
                    >
                    <input
                      type="text"
                      formControlName="address"
                      placeholder="e.g. 12 High St, Belfast BT1 1AA"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                    />
                  </label>
                  <button
                    type="submit"
                    [disabled]="venueBusy() || venueForm.invalid"
                    class="rounded-md bg-slate-900 dark:bg-amber-400 px-4 py-2 font-mono text-sm font-medium text-amber-300 dark:text-slate-900 disabled:opacity-50"
                  >
                    {{ venueBusy() ? 'Saving…' : editingVenueId() ? 'Save changes' : 'Add venue' }}
                  </button>
                  @if (venueError()) {
                    <p class="font-mono text-sm text-red-600 dark:text-red-400" role="alert">
                      {{ venueError() }}
                    </p>
                  }
                </form>
              </app-modal>
            </section>
          }

          @if (tabs.active() === 'matches') {
            <section role="tabpanel" id="panel-matches" aria-labelledby="tab-matches">
              <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
                Matches
              </h2>
              <ul
                class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900"
              >
                @for (m of matches(); track m.id) {
                  <li
                    class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 font-mono text-sm"
                  >
                    <span class="font-semibold">{{ m.matchDate }}</span>
                    <span
                      class="inline-block rounded bg-slate-200 dark:bg-slate-700 px-1.5 py-0.5 text-xs"
                      >{{ m.divisionName }}</span
                    >
                    <span>
                      {{ m.homeTeamName }}
                      @if (m.status === 'Played') {
                        <span class="font-semibold">{{ m.homeScore }}–{{ m.awayScore }}</span>
                      } @else {
                        <span class="text-slate-400 dark:text-slate-500">v</span>
                      }
                      {{ m.awayTeamName }}
                    </span>
                    @if (m.isWalkover) {
                      <span
                        class="rounded bg-amber-200 dark:bg-amber-900 px-1 text-xs text-amber-800 dark:text-amber-200"
                        >w/o</span
                      >
                    }
                    <span class="text-slate-500 dark:text-slate-400">@ {{ m.venueName }}</span>
                    <span
                      [class]="
                        'ml-auto inline-block rounded px-2 py-0.5 text-xs ' +
                        (m.status | statusColor)
                      "
                      >{{ m.status }}</span
                    >
                    @if (canManage() && m.status === 'Proposed') {
                      <span class="text-xs text-slate-400 dark:text-slate-500"
                        >({{ m.homeAccepted ? 'home ✓' : 'home …' }},
                        {{ m.awayAccepted ? 'away ✓' : 'away …' }})</span
                      >
                      <button
                        type="button"
                        (click)="onAcceptMatch(m)"
                        class="rounded-md border border-emerald-300 dark:border-emerald-800 px-2 py-1 text-xs text-emerald-700 dark:text-emerald-400 hover:bg-emerald-50 dark:hover:bg-emerald-950"
                      >
                        Accept
                      </button>
                      <button
                        type="button"
                        (click)="onRejectMatch(m)"
                        class="rounded-md border border-red-300 dark:border-red-800 px-2 py-1 text-xs text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950"
                      >
                        Reject
                      </button>
                    }
                    @if (canManage() && m.status === 'Confirmed') {
                      <button
                        type="button"
                        (click)="onOpenMatchResult(m)"
                        class="rounded-md border border-slate-300 dark:border-slate-700 px-2 py-1 text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                      >
                        Result
                      </button>
                      <button
                        type="button"
                        (click)="onMatchWalkover(m, 'Home')"
                        class="rounded-md border border-slate-300 dark:border-slate-700 px-2 py-1 text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                      >
                        W/O home
                      </button>
                      <button
                        type="button"
                        (click)="onMatchWalkover(m, 'Away')"
                        class="rounded-md border border-slate-300 dark:border-slate-700 px-2 py-1 text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                      >
                        W/O away
                      </button>
                    }
                    @if (matchResultId() === m.id) {
                      <form
                        [formGroup]="matchResultForm"
                        (ngSubmit)="onSaveMatchResult(m)"
                        class="flex w-full items-center gap-2 pt-1"
                      >
                        <input
                          type="number"
                          formControlName="homeScore"
                          min="0"
                          aria-label="Home score"
                          class="w-16 rounded-md border border-slate-300 dark:border-slate-700 px-2 py-1 text-xs dark:bg-slate-800 dark:text-slate-100"
                        />
                        <span class="text-slate-400 dark:text-slate-500">–</span>
                        <input
                          type="number"
                          formControlName="awayScore"
                          min="0"
                          aria-label="Away score"
                          class="w-16 rounded-md border border-slate-300 dark:border-slate-700 px-2 py-1 text-xs dark:bg-slate-800 dark:text-slate-100"
                        />
                        <button
                          type="submit"
                          class="rounded-md bg-slate-900 dark:bg-amber-400 px-2 py-1 text-xs font-medium text-amber-300 dark:text-slate-900"
                        >
                          Save
                        </button>
                        @if (matchError()) {
                          <span class="text-xs text-red-600 dark:text-red-400" role="alert">{{
                            matchError()
                          }}</span>
                        }
                      </form>
                    }
                  </li>
                } @empty {
                  <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                    No matches.
                  </li>
                }
              </ul>
            </section>
          }

          @if (tabs.active() === 'blocked') {
            <section role="tabpanel" id="panel-blocked" aria-labelledby="tab-blocked">
              <div class="mt-10 flex items-center justify-between">
                <h2 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
                  Blocked dates
                </h2>
                @if (canManage()) {
                  <button
                    type="button"
                    (click)="blockDialogOpen.set(true)"
                    class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-1 font-mono text-xs text-slate-700 dark:text-slate-300 hover:bg-slate-50 dark:hover:bg-slate-800"
                  >
                    ＋ Add blocked date
                  </button>
                }
              </div>
              <ul
                class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900"
              >
                @for (b of blockedDates(); track b.id) {
                  <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
                    <span>
                      <span
                        class="inline-block rounded bg-slate-200 dark:bg-slate-700 px-2 py-0.5 text-xs uppercase"
                        >{{ b.scope }}</span
                      >
                      @if (b.scope === 'Venue') {
                        <span class="ml-2 text-slate-600 dark:text-slate-400">{{
                          venueName(b.venueId)
                        }}</span>
                      }
                      @if (b.scope === 'Team') {
                        <span class="ml-2 text-slate-600 dark:text-slate-400">{{
                          teamName(b.teamId)
                        }}</span>
                      }
                      <span class="ml-2"
                        >{{ b.startDate }}
                        @if (b.endDate !== b.startDate) {
                          <span> → {{ b.endDate }}</span>
                        }
                      </span>
                      <span class="ml-2 text-slate-500 dark:text-slate-400">{{ b.reason }}</span>
                    </span>
                    @if (canManage()) {
                      <button
                        type="button"
                        [attr.aria-label]="'Delete blocked date ' + b.reason"
                        (click)="askDeleteBlockedDate(b)"
                        class="rounded-md border border-red-300 dark:border-red-800 px-3 py-1 text-xs text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950"
                      >
                        Delete
                      </button>
                    }
                  </li>
                } @empty {
                  <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                    No blocked dates.
                  </li>
                }
              </ul>

              <app-modal
                [open]="blockDialogOpen()"
                title="Add blocked date"
                (closed)="blockDialogOpen.set(false)"
              >
                <form [formGroup]="blockForm" (ngSubmit)="onCreateBlockedDate()" class="grid gap-3">
                  <div class="flex flex-wrap items-end gap-3">
                    <label class="grid gap-1">
                      <span
                        class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                        >Scope</span
                      >
                      <select
                        formControlName="scope"
                        class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                      >
                        <option value="Club">Club</option>
                        <option value="Venue">Venue</option>
                        <option value="Team">Team</option>
                      </select>
                    </label>

                    @if (blockForm.controls.scope.value === 'Venue') {
                      <label class="grid gap-1">
                        <span
                          class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                          >Venue</span
                        >
                        <select
                          formControlName="venueId"
                          class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                        >
                          <option value="">-- venue --</option>
                          @for (v of venues(); track v.id) {
                            <option [value]="v.id">{{ v.name }}</option>
                          }
                        </select>
                      </label>
                    }

                    @if (blockForm.controls.scope.value === 'Team') {
                      <label class="grid gap-1">
                        <span
                          class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                          >Team</span
                        >
                        <select
                          formControlName="teamId"
                          class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                        >
                          <option value="">-- team --</option>
                          @for (t of teams(); track t.id) {
                            <option [value]="t.id">{{ t.name }}</option>
                          }
                        </select>
                      </label>
                    }

                    <label class="grid gap-1">
                      <span
                        class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                        >From</span
                      >
                      <input
                        type="date"
                        formControlName="startDate"
                        class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                      />
                    </label>
                    <label class="grid gap-1">
                      <span
                        class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                        >To</span
                      >
                      <input
                        type="date"
                        formControlName="endDate"
                        class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                      />
                    </label>
                  </div>
                  <label class="grid gap-1">
                    <span
                      class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                      >Reason</span
                    >
                    <input
                      type="text"
                      formControlName="reason"
                      class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                      required
                    />
                  </label>
                  <button
                    type="submit"
                    [disabled]="blockBusy() || blockForm.invalid"
                    class="justify-self-start rounded-md bg-slate-900 dark:bg-amber-400 px-4 py-2 font-mono text-sm font-medium text-amber-300 dark:text-slate-900 disabled:opacity-50"
                  >
                    {{ blockBusy() ? 'Adding…' : 'Add blocked date' }}
                  </button>
                  @if (blockError()) {
                    <p class="font-mono text-sm text-red-600 dark:text-red-400" role="alert">
                      {{ blockError() }}
                    </p>
                  }
                </form>
              </app-modal>
            </section>
          }

          @if (tabs.active() === 'players') {
            <section role="tabpanel" id="panel-players" aria-labelledby="tab-players">
              @if (clubId()) {
                <app-club-players
                  [clubId]="clubId()"
                  [leagues]="acceptedLeagues()"
                  (playerCount)="playerCount.set($event)"
                />
              }
            </section>
          }

          @if (tabs.active() === 'sessions') {
            <section
              role="tabpanel"
              id="panel-sessions"
              aria-labelledby="tab-sessions"
              class="mt-8"
            >
              <app-pegboard-sessions [clubId]="clubId()" />
            </section>
          }
        } @else {
          <p class="py-10 text-center font-mono text-sm text-slate-500 dark:text-slate-400">
            Couldn't load this club.
          </p>
        }

        <app-confirm
          [message]="pending()?.message ?? null"
          (confirmed)="runPending()"
          (cancelled)="pending.set(null)"
        />

        <app-csv-import
          [open]="importKind() !== null"
          [title]="importKind() === 'venues' ? 'Import venues' : 'Import teams'"
          [columns]="importKind() === 'venues' ? venueImportColumns : teamImportColumns"
          [sample]="importKind() === 'venues' ? 'Main Hall,4,2,12 High St' : 'Acme 1st,Mens'"
          [result]="importResult()"
          [busy]="importBusy()"
          (submit)="onImport($event)"
          (closed)="closeImport()"
        />
      </main>
    </div>
  `,
})
export default class ClubDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ClubsApi);
  private readonly leagues = inject(LeaguesApi);
  private readonly toast = inject(ToastService);
  private readonly store = inject(AuthStore);

  protected readonly clubId = signal('');
  // Whether the current user may manage this club (SystemAdmin or a ClubAdmin grant for it).
  // Gates the admin controls; the backend enforces the same rule regardless.
  protected readonly canManage = computed(() => this.store.isClubAdmin(this.clubId()));
  private readonly playersApi = inject(PlayersApi);
  protected readonly playerCount = signal(0);
  protected readonly clubTabs = computed<TabDef[]>(() => [
    { id: 'teams', label: 'Teams', count: this.teams().length },
    { id: 'venues', label: 'Venues', count: this.venues().length },
    { id: 'players', label: 'Players', count: this.playerCount() },
    { id: 'sessions', label: 'Sessions' },
    { id: 'matches', label: 'Matches', count: this.matches().length },
    { id: 'blocked', label: 'Blocked dates', count: this.blockedDates().length },
    { id: 'admins', label: 'Admins', count: this.admins().length },
  ]);
  protected readonly club = signal<ClubDetail | null>(null);
  protected readonly loading = signal(true);
  // Membership / admin action ids currently in flight, so their buttons disable + busy.
  private readonly busyIds = signal<ReadonlySet<string>>(new Set());
  protected readonly admins = signal<ClubAdminSummary[]>([]);
  protected readonly memberships = signal<MembershipSummary[]>([]);
  protected readonly leagueList = signal<LeagueSummary[]>([]);
  protected readonly acceptedLeagues = computed(() =>
    this.memberships()
      .filter((m) => m.status === 'Accepted')
      .map((m) => ({ id: m.leagueId, name: this.leagueName(m.leagueId) })),
  );
  protected readonly teams = signal<TeamSummary[]>([]);
  protected readonly venues = signal<VenueSummary[]>([]);
  protected readonly blockedDates = signal<BlockedDateSummary[]>([]);
  protected readonly matches = signal<ClubMatch[]>([]);
  protected readonly matchResultId = signal<string | null>(null);
  protected readonly matchError = signal<string | null>(null);

  protected readonly matchResultForm = new FormGroup({
    homeScore: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
    awayScore: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
  });
  protected readonly adminBusy = signal(false);
  protected readonly adminError = signal<string | null>(null);
  protected readonly teamBusy = signal(false);
  protected readonly teamError = signal<string | null>(null);
  protected readonly venueBusy = signal(false);
  protected readonly venueError = signal<string | null>(null);
  protected readonly blockBusy = signal(false);
  protected readonly blockError = signal<string | null>(null);
  protected readonly adminDialogOpen = signal(false);
  protected readonly teamDialogOpen = signal(false);
  protected readonly squadTeamId = signal<string | null>(null);

  protected readonly importKind = signal<'teams' | 'venues' | null>(null);
  protected readonly importBusy = signal(false);
  protected readonly importResult = signal<ImportResult | null>(null);
  protected readonly teamImportColumns = ['name', 'gender'];
  protected readonly venueImportColumns = ['name', 'courts', 'maxConcurrentMatches', 'address'];
  protected readonly venueDialogOpen = signal(false);
  protected readonly editingVenueId = signal<string | null>(null);
  protected readonly blockDialogOpen = signal(false);
  protected readonly pending = signal<{ message: string; action: () => void } | null>(null);

  protected runPending(): void {
    const p = this.pending();
    this.pending.set(null);
    p?.action();
  }

  protected askRevoke(admin: ClubAdminSummary): void {
    this.pending.set({
      message: `Revoke ${admin.email} as a club admin?`,
      action: () => this.onRevoke(admin.userId),
    });
  }

  protected askDeleteTeam(t: TeamSummary): void {
    this.pending.set({ message: `Delete team "${t.name}"?`, action: () => this.onDeleteTeam(t) });
  }

  protected askDeleteVenue(v: VenueSummary): void {
    this.pending.set({ message: `Delete venue "${v.name}"?`, action: () => this.onDeleteVenue(v) });
  }

  protected askDeleteBlockedDate(b: BlockedDateSummary): void {
    this.pending.set({
      message: `Delete this blocked date (${b.reason})?`,
      action: () => this.onDeleteBlockedDate(b),
    });
  }

  protected readonly adminForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
  });

  protected readonly teamForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    gender: new FormControl<Gender>('Mens', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  protected readonly venueForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    courts: new FormControl(2, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(1)],
    }),
    maxConcurrentMatches: new FormControl(1, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    address: new FormControl('', { nonNullable: true }),
  });

  // Google Maps search link for a venue's free-text address.
  protected mapUrl(address: string): string {
    return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(address)}`;
  }

  protected readonly blockForm = new FormGroup({
    scope: new FormControl<BlockedDateScope>('Club', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    venueId: new FormControl('', { nonNullable: true }),
    teamId: new FormControl('', { nonNullable: true }),
    startDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    endDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    this.route.paramMap
      .pipe(
        tap((p) => {
          this.clubId.set(p.get('id') ?? '');
          this.loading.set(true);
        }),
        switchMap((p) => this.api.get(p.get('id') ?? '')),
        tap((c) => this.club.set(c)),
      )
      .subscribe({
        next: () => {
          this.loading.set(false);
          this.refreshAdmins();
          this.refreshMemberships();
          this.refreshTeams();
          this.refreshVenues();
          this.refreshBlockedDates();
          this.refreshMatches();
          this.refreshPlayerCount();
        },
        error: () => this.loading.set(false),
      });
    this.leagues.list().subscribe({ next: (rows) => this.leagueList.set(rows) });
  }

  protected leagueName(leagueId: string): string {
    return this.leagueList().find((l) => l.id === leagueId)?.name ?? leagueId;
  }

  private refreshAdmins(): void {
    this.api.listAdmins(this.clubId()).subscribe({
      next: (rows) => this.admins.set(rows),
    });
  }

  private refreshMemberships(): void {
    this.api.listMemberships(this.clubId()).subscribe({
      next: (rows) => this.memberships.set(rows),
    });
  }

  protected onGrant(): void {
    const email = this.adminForm.getRawValue().email.trim();
    if (!email) return;
    this.adminBusy.set(true);
    this.adminError.set(null);
    this.leagues.lookupUser(email).subscribe({
      next: (user) => {
        this.api.grantAdmin(this.clubId(), user.id).subscribe({
          next: () => {
            this.adminBusy.set(false);
            this.adminForm.reset({ email: '' });
            this.adminDialogOpen.set(false);
            this.toast.success(`Granted ${email} as a club admin.`);
            this.refreshAdmins();
          },
          error: (err: { error?: { title?: string } }) => {
            this.adminBusy.set(false);
            this.adminError.set(err?.error?.title ?? 'Grant failed.');
          },
        });
      },
      error: () => {
        this.adminBusy.set(false);
        this.adminError.set('No registered user with that email.');
      },
    });
  }

  protected onRevoke(userId: string): void {
    this.api.revokeAdmin(this.clubId(), userId).subscribe({
      next: () => this.refreshAdmins(),
    });
  }

  protected onAccept(m: MembershipSummary): void {
    this.membershipAction(m, this.api.acceptMembership(m.leagueId, m.id), 'Accepted');
  }

  protected onDecline(m: MembershipSummary): void {
    this.membershipAction(m, this.api.declineMembership(m.leagueId, m.id), 'Declined');
  }

  protected onWithdraw(m: MembershipSummary): void {
    this.membershipAction(m, this.api.withdrawMembership(m.leagueId, m.id), 'Withdrew from');
  }

  // Whether an inline action keyed by this id is mid-request (drives button disable + busy text).
  protected isBusy(id: string): boolean {
    return this.busyIds().has(id);
  }

  private setBusy(id: string, on: boolean): void {
    this.busyIds.update((s) => {
      const next = new Set(s);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  private membershipAction(m: MembershipSummary, action: Observable<void>, verb: string): void {
    if (this.isBusy(m.id)) return;
    this.setBusy(m.id, true);
    action.subscribe({
      next: () => {
        this.setBusy(m.id, false);
        this.toast.success(`${verb} ${this.leagueName(m.leagueId)}.`);
        this.refreshMemberships();
      },
      error: (err: { error?: { title?: string } }) => {
        this.setBusy(m.id, false);
        this.toast.error(err?.error?.title ?? 'That action failed.');
      },
    });
  }

  protected toggleSquad(teamId: string): void {
    this.squadTeamId.set(this.squadTeamId() === teamId ? null : teamId);
  }

  protected openImport(kind: 'teams' | 'venues'): void {
    this.importResult.set(null);
    this.importKind.set(kind);
  }

  protected closeImport(): void {
    this.importKind.set(null);
    this.importResult.set(null);
  }

  protected onImport(csv: string): void {
    const kind = this.importKind();
    if (kind === null) return;
    this.importBusy.set(true);
    const request =
      kind === 'venues'
        ? this.api.importVenues(this.clubId(), csv)
        : this.api.importTeams(this.clubId(), csv);
    request.subscribe({
      next: (result) => {
        this.importBusy.set(false);
        this.importResult.set(result);
        if (kind === 'venues') this.refreshVenues();
        else this.refreshTeams();
      },
      error: () => {
        this.importBusy.set(false);
        this.importResult.set({
          created: 0,
          updated: 0,
          errors: [{ row: 0, message: 'Import failed.' }],
        });
      },
    });
  }

  private refreshTeams(): void {
    this.api.listTeams(this.clubId()).subscribe({ next: (rows) => this.teams.set(rows) });
  }

  private refreshVenues(): void {
    this.api.listVenues(this.clubId()).subscribe({ next: (rows) => this.venues.set(rows) });
  }

  protected onCreateTeam(): void {
    const { name, gender } = this.teamForm.getRawValue();
    const trimmed = name.trim();
    if (!trimmed) return;
    this.teamBusy.set(true);
    this.teamError.set(null);
    this.api.createTeam(this.clubId(), trimmed, gender).subscribe({
      next: () => {
        this.teamBusy.set(false);
        this.teamForm.reset({ name: '', gender: 'Mens' });
        this.teamDialogOpen.set(false);
        this.refreshTeams();
      },
      error: (err: { error?: { title?: string } }) => {
        this.teamBusy.set(false);
        this.teamError.set(err?.error?.title ?? 'Could not add team.');
      },
    });
  }

  protected onDeleteTeam(t: TeamSummary): void {
    this.api.deleteTeam(this.clubId(), t.id).subscribe({ next: () => this.refreshTeams() });
  }

  protected openEditVenue(v: VenueSummary): void {
    this.venueError.set(null);
    this.editingVenueId.set(v.id);
    this.venueForm.reset({
      name: v.name,
      courts: v.courts,
      maxConcurrentMatches: v.maxConcurrentMatches,
      address: v.address ?? '',
    });
    this.venueDialogOpen.set(true);
  }

  protected closeVenueDialog(): void {
    this.venueDialogOpen.set(false);
    this.editingVenueId.set(null);
    this.venueForm.reset({ name: '', courts: 2, maxConcurrentMatches: 1, address: '' });
  }

  protected onSaveVenue(): void {
    const { name, courts, maxConcurrentMatches, address } = this.venueForm.getRawValue();
    const trimmed = name.trim();
    if (!trimmed) return;
    this.venueBusy.set(true);
    this.venueError.set(null);
    const editingId = this.editingVenueId();
    const done = (msg: string) => {
      this.venueBusy.set(false);
      this.closeVenueDialog();
      this.toast.success(msg);
      this.refreshVenues();
    };
    const fail = (err: { error?: { title?: string } }) => {
      this.venueBusy.set(false);
      this.venueError.set(err?.error?.title ?? 'Could not save venue.');
    };
    if (editingId) {
      this.api
        .updateVenue(
          this.clubId(),
          editingId,
          trimmed,
          Number(courts),
          Number(maxConcurrentMatches),
          address.trim() || null,
        )
        .subscribe({ next: () => done(`Venue “${trimmed}” updated.`), error: fail });
    } else {
      this.api
        .createVenue(
          this.clubId(),
          trimmed,
          Number(courts),
          Number(maxConcurrentMatches),
          address.trim() || null,
        )
        .subscribe({ next: () => done(`Venue “${trimmed}” added.`), error: fail });
    }
  }

  protected onDeleteVenue(v: VenueSummary): void {
    this.api.deleteVenue(this.clubId(), v.id).subscribe({ next: () => this.refreshVenues() });
  }

  private refreshBlockedDates(): void {
    this.api
      .listBlockedDates(this.clubId())
      .subscribe({ next: (rows) => this.blockedDates.set(rows) });
  }

  protected venueName(id: string | null): string {
    return this.venues().find((v) => v.id === id)?.name ?? '(venue)';
  }

  protected teamName(id: string | null): string {
    return this.teams().find((t) => t.id === id)?.name ?? '(team)';
  }

  protected onCreateBlockedDate(): void {
    const v = this.blockForm.getRawValue();
    const reason = v.reason.trim();
    if (!reason) return;
    if (v.scope === 'Venue' && !v.venueId) {
      this.blockError.set('Choose a venue.');
      return;
    }
    if (v.scope === 'Team' && !v.teamId) {
      this.blockError.set('Choose a team.');
      return;
    }

    this.blockBusy.set(true);
    this.blockError.set(null);
    this.api
      .createBlockedDate(this.clubId(), {
        scope: v.scope,
        venueId: v.scope === 'Venue' ? v.venueId : null,
        teamId: v.scope === 'Team' ? v.teamId : null,
        startDate: v.startDate,
        endDate: v.endDate,
        reason,
      })
      .subscribe({
        next: () => {
          this.blockBusy.set(false);
          this.blockForm.reset({
            scope: 'Club',
            venueId: '',
            teamId: '',
            startDate: '',
            endDate: '',
            reason: '',
          });
          this.blockDialogOpen.set(false);
          this.refreshBlockedDates();
        },
        error: (err: { error?: { title?: string } }) => {
          this.blockBusy.set(false);
          this.blockError.set(err?.error?.title ?? 'Could not add blocked date.');
        },
      });
  }

  protected onDeleteBlockedDate(b: BlockedDateSummary): void {
    this.api
      .deleteBlockedDate(this.clubId(), b.id)
      .subscribe({ next: () => this.refreshBlockedDates() });
  }

  // Eager count for the Players tab pill (the players child only mounts when that tab is open).
  private refreshPlayerCount(): void {
    this.playersApi
      .listClubPlayers(this.clubId())
      .subscribe({ next: (p) => this.playerCount.set(p.length) });
  }

  private refreshMatches(): void {
    this.api.listMatches(this.clubId()).subscribe({ next: (rows) => this.matches.set(rows) });
  }

  protected onAcceptMatch(m: ClubMatch): void {
    this.api.acceptMatch(m.id).subscribe({ next: () => this.refreshMatches() });
  }

  protected onRejectMatch(m: ClubMatch): void {
    this.api.rejectMatch(m.id).subscribe({ next: () => this.refreshMatches() });
  }

  protected onOpenMatchResult(m: ClubMatch): void {
    this.matchError.set(null);
    this.matchResultForm.reset({ homeScore: 0, awayScore: 0 });
    this.matchResultId.set(this.matchResultId() === m.id ? null : m.id);
  }

  protected onSaveMatchResult(m: ClubMatch): void {
    const { homeScore, awayScore } = this.matchResultForm.getRawValue();
    this.matchError.set(null);
    this.api.recordResult(m.id, Number(homeScore), Number(awayScore), m.matchDate).subscribe({
      next: () => {
        this.matchResultId.set(null);
        this.refreshMatches();
      },
      error: (err: { error?: { title?: string } }) =>
        this.matchError.set(err?.error?.title ?? 'Could not record result.'),
    });
  }

  protected onMatchWalkover(m: ClubMatch, winner: 'Home' | 'Away'): void {
    this.api.recordWalkover(m.id, winner).subscribe({ next: () => this.refreshMatches() });
  }
}
