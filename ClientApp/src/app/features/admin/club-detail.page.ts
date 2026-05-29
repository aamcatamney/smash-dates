import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import {
  BlockedDateScope,
  BlockedDateSummary,
  ClubAdminSummary,
  ClubDetail,
  ClubsApi,
  Gender,
  MembershipSummary,
  TeamSummary,
  VenueSummary,
} from './clubs.api';
import { LeaguesApi } from './leagues.api';
import { AdminHeaderComponent } from './admin-header.component';

@Component({
  selector: 'app-club-detail-page',
  imports: [ReactiveFormsModule, RouterLink, AdminHeaderComponent],
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

        <h2 class="mt-8 font-mono text-lg font-semibold text-slate-900">Club admins</h2>
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

        <form
          [formGroup]="adminForm"
          (ngSubmit)="onGrant()"
          class="mt-4 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
        >
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

        <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900">Teams</h2>
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

        <form
          [formGroup]="teamForm"
          (ngSubmit)="onCreateTeam()"
          class="mt-4 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm sm:grid-cols-[1fr_auto_auto] sm:items-end"
        >
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
            <p class="font-mono text-sm text-red-600 sm:col-span-3" role="alert">{{ teamError() }}</p>
          }
        </form>

        <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900">Venues</h2>
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

        <form
          [formGroup]="venueForm"
          (ngSubmit)="onCreateVenue()"
          class="mt-4 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm sm:grid-cols-[1fr_auto_auto] sm:items-end"
        >
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
            <p class="font-mono text-sm text-red-600 sm:col-span-3" role="alert">{{ venueError() }}</p>
          }
        </form>

        <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900">Blocked dates</h2>
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

        <form
          [formGroup]="blockForm"
          (ngSubmit)="onCreateBlockedDate()"
          class="mt-4 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
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
  protected readonly adminBusy = signal(false);
  protected readonly adminError = signal<string | null>(null);
  protected readonly teamBusy = signal(false);
  protected readonly teamError = signal<string | null>(null);
  protected readonly venueBusy = signal(false);
  protected readonly venueError = signal<string | null>(null);
  protected readonly blockBusy = signal(false);
  protected readonly blockError = signal<string | null>(null);

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
}
