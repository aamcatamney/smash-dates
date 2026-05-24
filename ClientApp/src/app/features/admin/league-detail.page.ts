import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import {
  CreateDivisionRequest,
  DivisionGender,
  DivisionSummary,
  LeagueDetail,
  LeaguesApi,
} from './leagues.api';

@Component({
  selector: 'app-league-detail-page',
  imports: [ReactiveFormsModule],
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
      </main>
    </div>
  `,
})
export default class LeagueDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(LeaguesApi);

  protected readonly league = signal<LeagueDetail | null>(null);
  protected readonly divisions = signal<DivisionSummary[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly submitting = signal(false);
  private leagueId = '';

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
        switchMap((l) => this.api.listDivisions(l.id)),
      )
      .subscribe({
        next: (rows) => this.divisions.set(rows),
        error: () => this.error.set('Failed to load league.'),
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
