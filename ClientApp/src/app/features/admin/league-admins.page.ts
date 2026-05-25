import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import { LeagueAdminSummary, LeaguesApi } from './leagues.api';

@Component({
  selector: 'app-league-admins-page',
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
        <a
          [routerLink]="['/admin/leagues', leagueId()]"
          class="font-mono text-xs uppercase tracking-wider text-slate-500 hover:underline"
          >← back to league</a
        >
        <h1 class="mt-2 font-mono text-2xl font-semibold text-slate-900">League admins</h1>

        <ul class="mt-6 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (admin of admins(); track admin.userId) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                {{ admin.displayName ?? admin.email }}
                <span class="ml-2 text-slate-500">{{ admin.email }}</span>
              </span>
              <button
                type="button"
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
          [formGroup]="form"
          (ngSubmit)="onGrant()"
          class="mt-6 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
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
            [disabled]="submitting() || form.invalid"
            class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ submitting() ? 'Granting…' : 'Grant admin' }}
          </button>
          @if (error()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ error() }}</p>
          }
        </form>
      </main>
    </div>
  `,
})
export default class LeagueAdminsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(LeaguesApi);

  protected readonly leagueId = signal('');
  protected readonly admins = signal<LeagueAdminSummary[]>([]);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
  });

  constructor() {
    this.route.paramMap
      .pipe(
        tap((p) => this.leagueId.set(p.get('id') ?? '')),
        switchMap((p) => this.api.listAdmins(p.get('id') ?? '')),
      )
      .subscribe({
        next: (rows) => this.admins.set(rows),
        error: () => this.error.set('Failed to load admins.'),
      });
  }

  protected onGrant(): void {
    const email = this.form.getRawValue().email.trim();
    if (!email) return;
    this.submitting.set(true);
    this.error.set(null);
    this.api.lookupUser(email).subscribe({
      next: (user) => {
        this.api.grantAdmin(this.leagueId(), user.id).subscribe({
          next: () => {
            this.submitting.set(false);
            this.form.reset({ email: '' });
            this.refresh();
          },
          error: (err: { error?: { title?: string } }) => {
            this.submitting.set(false);
            this.error.set(err?.error?.title ?? 'Grant failed.');
          },
        });
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('No registered user with that email.');
      },
    });
  }

  protected onRevoke(userId: string): void {
    this.error.set(null);
    this.api.revokeAdmin(this.leagueId(), userId).subscribe({
      next: () => this.refresh(),
      error: (err: { error?: { title?: string } }) =>
        this.error.set(err?.error?.title ?? 'Revoke failed.'),
    });
  }

  private refresh(): void {
    this.api.listAdmins(this.leagueId()).subscribe({
      next: (rows) => this.admins.set(rows),
    });
  }
}
