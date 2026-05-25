import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import {
  CreateDivisionRequest,
  DivisionGender,
  DivisionSummary,
  LeagueDetail,
  LeaguesApi,
  MembershipSummary,
} from './leagues.api';
import { ClubsApi, ClubSummary } from './clubs.api';

@Component({
  selector: 'app-league-detail-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50">
      <header class="border-b border-slate-200 bg-white">
        <div class="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <span class="font-mono text-sm font-semibold tracking-wide text-slate-900">smash-dates / admin</span>
        </div>
      </header>

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

        <h2 class="mt-8 font-mono text-lg font-semibold text-slate-900">Divisions</h2>
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

        <form
          [formGroup]="form"
          (ngSubmit)="onCreate()"
          class="mt-6 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
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

        <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900">Member clubs</h2>
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

        <form
          [formGroup]="inviteForm"
          (ngSubmit)="onInvite()"
          class="mt-4 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
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
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected leagueId = '';

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
}
