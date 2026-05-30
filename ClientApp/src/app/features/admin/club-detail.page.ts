import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, tap } from 'rxjs';
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
import { LeaguesApi } from './leagues.api';
import { AdminHeaderComponent } from './admin-header.component';
import { ModalComponent } from '../../shared/modal.component';

@Component({
  selector: 'app-club-detail-page',
  imports: [ReactiveFormsModule, RouterLink, AdminHeaderComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <a
          [routerLink]="['/admin/clubs']"
          class="font-mono text-xs uppercase tracking-wider text-slate-500 hover:underline"
          >← back to clubs</a
        >

        @if (club(); as c) {
          <h1 class="mt-2 font-mono text-2xl font-semibold text-slate-900">
            {{ c.shortCode }} · {{ c.name }}
          </h1>
          <p class="mt-1 font-mono text-sm text-slate-500">{{ c.contactEmail }}</p>
          @if (c.notes) {
            <p class="mt-1 font-mono text-sm text-slate-500">{{ c.notes }}</p>
          }
        }

        <div class="mt-8 flex items-center justify-between">
          <h2 class="font-mono text-lg font-semibold text-slate-900">Club admins</h2>
          <button
            type="button"
            (click)="adminDialogOpen.set(true)"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50"
          >
            ＋ Add admin
          </button>
        </div>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (admin of admins(); track admin.userId) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                {{ admin.displayName ?? admin.email }}
                <span class="ml-2 text-slate-500">{{ admin.email }}</span>
              </span>
              <button
                type="button"
                [attr.aria-label]="'Revoke ' + admin.email"
                (click)="onRevoke(admin.userId)"
                class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
              >
                Revoke
              </button>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No admins.</li>
          }
        </ul>

        <app-modal [open]="adminDialogOpen()" title="Add club admin" (closed)="adminDialogOpen.set(false)">
        <form [formGroup]="adminForm" (ngSubmit)="onGrant()" class="grid gap-3">
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Add admin by email</span>
            <input
              type="email"
              formControlName="email"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              required
            />
          </label>
          <button
            type="submit"
            [disabled]="adminBusy() || adminForm.invalid"
            class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ adminBusy() ? 'Granting…' : 'Grant admin' }}
          </button>
          @if (adminError()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ adminError() }}</p>
          }
        </form>
        </app-modal>

        <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900">League memberships</h2>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (m of memberships(); track m.id) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                league <span class="text-slate-500">{{ m.leagueId }}</span>
                <span class="ml-3 inline-block rounded bg-slate-200 px-2 py-0.5 text-xs">{{ m.status }}</span>
              </span>
              <div class="flex gap-2">
                @if (m.status === 'Pending') {
                  <button
                    type="button"
                    (click)="onAccept(m)"
                    class="rounded-md border border-emerald-300 px-3 py-1 text-xs text-emerald-700 hover:bg-emerald-50"
                  >Accept</button>
                  <button
                    type="button"
                    (click)="onDecline(m)"
                    class="rounded-md border border-amber-300 px-3 py-1 text-xs text-amber-700 hover:bg-amber-50"
                  >Decline</button>
                }
                @if (m.status === 'Accepted') {
                  <button
                    type="button"
                    (click)="onWithdraw(m)"
                    class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
                  >Withdraw</button>
                }
              </div>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No memberships.</li>
          }
        </ul>

        <div class="mt-10 flex items-center justify-between">
          <h2 class="font-mono text-lg font-semibold text-slate-900">Teams</h2>
          <button
            type="button"
            (click)="teamDialogOpen.set(true)"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50"
          >
            ＋ Add team
          </button>
        </div>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (t of teams(); track t.id) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                {{ t.name }}
                <span class="ml-2 inline-block rounded bg-slate-200 px-2 py-0.5 text-xs uppercase">{{ t.gender }}</span>
              </span>
              <button
                type="button"
                [attr.aria-label]="'Delete team ' + t.name"
                (click)="onDeleteTeam(t)"
                class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
              >
                Delete
              </button>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No teams.</li>
          }
        </ul>

        <app-modal [open]="teamDialogOpen()" title="Add team" (closed)="teamDialogOpen.set(false)">
        <form [formGroup]="teamForm" (ngSubmit)="onCreateTeam()" class="grid gap-3">
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Team name</span>
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
          <button
            type="submit"
            [disabled]="teamBusy() || teamForm.invalid"
            class="rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ teamBusy() ? 'Adding…' : 'Add team' }}
          </button>
          @if (teamError()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ teamError() }}</p>
          }
        </form>
        </app-modal>

        <div class="mt-10 flex items-center justify-between">
          <h2 class="font-mono text-lg font-semibold text-slate-900">Venues</h2>
          <button
            type="button"
            (click)="venueDialogOpen.set(true)"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50"
          >
            ＋ Add venue
          </button>
        </div>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (v of venues(); track v.id) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                {{ v.name }}
                <span class="ml-2 inline-block rounded bg-slate-200 px-2 py-0.5 text-xs">
                  {{ v.capacity }} {{ v.capacity === 1 ? 'court' : 'courts' }}
                </span>
              </span>
              <button
                type="button"
                [attr.aria-label]="'Delete venue ' + v.name"
                (click)="onDeleteVenue(v)"
                class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
              >
                Delete
              </button>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No venues.</li>
          }
        </ul>

        <app-modal [open]="venueDialogOpen()" title="Add venue" (closed)="venueDialogOpen.set(false)">
        <form [formGroup]="venueForm" (ngSubmit)="onCreateVenue()" class="grid gap-3">
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Venue name</span>
            <input
              type="text"
              formControlName="name"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              required
            />
          </label>
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Courts</span>
            <select
              formControlName="capacity"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
            >
              <option [value]="1">1</option>
              <option [value]="2">2</option>
            </select>
          </label>
          <button
            type="submit"
            [disabled]="venueBusy() || venueForm.invalid"
            class="rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ venueBusy() ? 'Adding…' : 'Add venue' }}
          </button>
          @if (venueError()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ venueError() }}</p>
          }
        </form>
        </app-modal>

        <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900">Matches</h2>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (m of matches(); track m.id) {
            <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 font-mono text-sm">
              <span class="font-semibold">{{ m.matchDate }}</span>
              <span class="inline-block rounded bg-slate-200 px-1.5 py-0.5 text-xs">{{ m.divisionName }}</span>
              <span>
                {{ m.homeTeamName }}
                @if (m.status === 'Played') {
                  <span class="font-semibold">{{ m.homeScore }}–{{ m.awayScore }}</span>
                } @else {
                  <span class="text-slate-400">v</span>
                }
                {{ m.awayTeamName }}
              </span>
              @if (m.isWalkover) { <span class="rounded bg-amber-200 px-1 text-xs text-amber-800">w/o</span> }
              <span class="text-slate-500">@ {{ m.venueName }}</span>
              <span class="ml-auto inline-block rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600">{{ m.status }}</span>
              @if (m.status === 'Proposed') {
                <span class="text-xs text-slate-400">({{ m.homeAccepted ? 'home ✓' : 'home …' }}, {{ m.awayAccepted ? 'away ✓' : 'away …' }})</span>
                <button type="button" (click)="onAcceptMatch(m)" class="rounded-md border border-emerald-300 px-2 py-1 text-xs text-emerald-700 hover:bg-emerald-50">Accept</button>
                <button type="button" (click)="onRejectMatch(m)" class="rounded-md border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50">Reject</button>
              }
              @if (m.status === 'Confirmed') {
                <button type="button" (click)="onOpenMatchResult(m)" class="rounded-md border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50">Result</button>
                <button type="button" (click)="onMatchWalkover(m, 'Home')" class="rounded-md border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50">W/O home</button>
                <button type="button" (click)="onMatchWalkover(m, 'Away')" class="rounded-md border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50">W/O away</button>
              }
              @if (matchResultId() === m.id) {
                <form [formGroup]="matchResultForm" (ngSubmit)="onSaveMatchResult(m)" class="flex w-full items-center gap-2 pt-1">
                  <input type="number" formControlName="homeScore" min="0" aria-label="Home score" class="w-16 rounded-md border border-slate-300 px-2 py-1 text-xs" />
                  <span class="text-slate-400">–</span>
                  <input type="number" formControlName="awayScore" min="0" aria-label="Away score" class="w-16 rounded-md border border-slate-300 px-2 py-1 text-xs" />
                  <button type="submit" class="rounded-md bg-slate-900 px-2 py-1 text-xs font-medium text-amber-300">Save</button>
                  @if (matchError()) { <span class="text-xs text-red-600" role="alert">{{ matchError() }}</span> }
                </form>
              }
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No matches.</li>
          }
        </ul>

        <div class="mt-10 flex items-center justify-between">
          <h2 class="font-mono text-lg font-semibold text-slate-900">Blocked dates</h2>
          <button
            type="button"
            (click)="blockDialogOpen.set(true)"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50"
          >
            ＋ Add blocked date
          </button>
        </div>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (b of blockedDates(); track b.id) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                <span class="inline-block rounded bg-slate-200 px-2 py-0.5 text-xs uppercase">{{ b.scope }}</span>
                @if (b.scope === 'Venue') { <span class="ml-2 text-slate-600">{{ venueName(b.venueId) }}</span> }
                @if (b.scope === 'Team') { <span class="ml-2 text-slate-600">{{ teamName(b.teamId) }}</span> }
                <span class="ml-2">{{ b.startDate }}@if (b.endDate !== b.startDate) { <span> → {{ b.endDate }}</span> }</span>
                <span class="ml-2 text-slate-500">{{ b.reason }}</span>
              </span>
              <button
                type="button"
                [attr.aria-label]="'Delete blocked date ' + b.reason"
                (click)="onDeleteBlockedDate(b)"
                class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
              >
                Delete
              </button>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No blocked dates.</li>
          }
        </ul>

        <app-modal [open]="blockDialogOpen()" title="Add blocked date" (closed)="blockDialogOpen.set(false)">
        <form
          [formGroup]="blockForm"
          (ngSubmit)="onCreateBlockedDate()"
          class="grid gap-3"
        >
          <div class="flex flex-wrap items-end gap-3">
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Scope</span>
              <select
                formControlName="scope"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              >
                <option value="Club">Club</option>
                <option value="Venue">Venue</option>
                <option value="Team">Team</option>
              </select>
            </label>

            @if (blockForm.controls.scope.value === 'Venue') {
              <label class="grid gap-1">
                <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Venue</span>
                <select
                  formControlName="venueId"
                  class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
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
                <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Team</span>
                <select
                  formControlName="teamId"
                  class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
                >
                  <option value="">-- team --</option>
                  @for (t of teams(); track t.id) {
                    <option [value]="t.id">{{ t.name }}</option>
                  }
                </select>
              </label>
            }

            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">From</span>
              <input
                type="date"
                formControlName="startDate"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">To</span>
              <input
                type="date"
                formControlName="endDate"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
            </label>
          </div>
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Reason</span>
            <input
              type="text"
              formControlName="reason"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              required
            />
          </label>
          <button
            type="submit"
            [disabled]="blockBusy() || blockForm.invalid"
            class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ blockBusy() ? 'Adding…' : 'Add blocked date' }}
          </button>
          @if (blockError()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ blockError() }}</p>
          }
        </form>
        </app-modal>
      </main>
    </div>
  `,
})
export default class ClubDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ClubsApi);
  private readonly leagues = inject(LeaguesApi);

  protected readonly clubId = signal('');
  protected readonly club = signal<ClubDetail | null>(null);
  protected readonly admins = signal<ClubAdminSummary[]>([]);
  protected readonly memberships = signal<MembershipSummary[]>([]);
  protected readonly teams = signal<TeamSummary[]>([]);
  protected readonly venues = signal<VenueSummary[]>([]);
  protected readonly blockedDates = signal<BlockedDateSummary[]>([]);
  protected readonly matches = signal<ClubMatch[]>([]);
  protected readonly matchResultId = signal<string | null>(null);
  protected readonly matchError = signal<string | null>(null);

  protected readonly matchResultForm = new FormGroup({
    homeScore: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
    awayScore: new FormControl(0, { nonNullable: true, validators: [Validators.required, Validators.min(0)] }),
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
  protected readonly venueDialogOpen = signal(false);
  protected readonly blockDialogOpen = signal(false);

  protected readonly adminForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
  });

  protected readonly teamForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    gender: new FormControl<Gender>('Mens', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly venueForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    capacity: new FormControl(1, { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly blockForm = new FormGroup({
    scope: new FormControl<BlockedDateScope>('Club', { nonNullable: true, validators: [Validators.required] }),
    venueId: new FormControl('', { nonNullable: true }),
    teamId: new FormControl('', { nonNullable: true }),
    startDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    endDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    this.route.paramMap
      .pipe(
        tap((p) => this.clubId.set(p.get('id') ?? '')),
        switchMap((p) => this.api.get(p.get('id') ?? '')),
        tap((c) => this.club.set(c)),
      )
      .subscribe({
        next: () => {
          this.refreshAdmins();
          this.refreshMemberships();
          this.refreshTeams();
          this.refreshVenues();
          this.refreshBlockedDates();
          this.refreshMatches();
        },
      });
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
    this.api.acceptMembership(m.leagueId, m.id).subscribe({ next: () => this.refreshMemberships() });
  }

  protected onDecline(m: MembershipSummary): void {
    this.api.declineMembership(m.leagueId, m.id).subscribe({ next: () => this.refreshMemberships() });
  }

  protected onWithdraw(m: MembershipSummary): void {
    this.api.withdrawMembership(m.leagueId, m.id).subscribe({ next: () => this.refreshMemberships() });
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

  protected onCreateVenue(): void {
    const { name, capacity } = this.venueForm.getRawValue();
    const trimmed = name.trim();
    if (!trimmed) return;
    this.venueBusy.set(true);
    this.venueError.set(null);
    this.api.createVenue(this.clubId(), trimmed, Number(capacity)).subscribe({
      next: () => {
        this.venueBusy.set(false);
        this.venueForm.reset({ name: '', capacity: 1 });
        this.venueDialogOpen.set(false);
        this.refreshVenues();
      },
      error: (err: { error?: { title?: string } }) => {
        this.venueBusy.set(false);
        this.venueError.set(err?.error?.title ?? 'Could not add venue.');
      },
    });
  }

  protected onDeleteVenue(v: VenueSummary): void {
    this.api.deleteVenue(this.clubId(), v.id).subscribe({ next: () => this.refreshVenues() });
  }

  private refreshBlockedDates(): void {
    this.api.listBlockedDates(this.clubId()).subscribe({ next: (rows) => this.blockedDates.set(rows) });
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
          this.blockForm.reset({ scope: 'Club', venueId: '', teamId: '', startDate: '', endDate: '', reason: '' });
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
    this.api.deleteBlockedDate(this.clubId(), b.id).subscribe({ next: () => this.refreshBlockedDates() });
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
