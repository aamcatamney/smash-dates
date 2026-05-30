import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import { LeagueAdminSummary, LeaguesApi } from './leagues.api';
import { AdminHeaderComponent } from './admin-header.component';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmComponent } from '../../shared/confirm.component';

@Component({
  selector: 'app-league-admins-page',
  imports: [ReactiveFormsModule, RouterLink, AdminHeaderComponent, ModalComponent, ConfirmComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50 dark:bg-slate-950">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <a
          [routerLink]="['/admin/leagues', leagueId()]"
          class="font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400 hover:underline"
          >← back to league</a
        >
        <div class="mt-2 flex items-center justify-between">
          <h1 class="font-mono text-2xl font-semibold text-slate-900 dark:text-slate-100">League admins</h1>
          <button
            type="button"
            (click)="dialogOpen.set(true)"
            class="rounded-md bg-slate-900 dark:bg-amber-400 px-4 py-2 font-mono text-sm font-medium text-amber-300 dark:text-slate-900 hover:bg-slate-800 dark:hover:bg-amber-300"
          >
            ＋ Add admin
          </button>
        </div>

        <ul class="mt-6 divide-y divide-slate-200 rounded-md border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
          @for (admin of admins(); track admin.userId) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                {{ admin.displayName ?? admin.email }}
                <span class="ml-2 text-slate-500 dark:text-slate-400">{{ admin.email }}</span>
              </span>
              <button
                type="button"
                (click)="askRevoke(admin)"
                class="rounded-md border border-red-300 dark:border-red-800 px-3 py-1 text-xs text-red-700 dark:text-red-400 hover:bg-red-50 dark:hover:bg-red-950"
              >
                Revoke
              </button>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">No admins.</li>
          }
        </ul>

        <app-modal [open]="dialogOpen()" title="Add league admin" (closed)="dialogOpen.set(false)">
          <form [formGroup]="form" (ngSubmit)="onGrant()" class="grid gap-3">
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Add admin by email</span>
              <input
                type="email"
                formControlName="email"
                class="rounded-md border border-slate-300 dark:border-slate-700 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                required
              />
            </label>
            <button
              type="submit"
              [disabled]="submitting() || form.invalid"
              class="justify-self-start rounded-md bg-slate-900 dark:bg-amber-400 px-4 py-2 font-mono text-sm font-medium text-amber-300 dark:text-slate-900 disabled:opacity-50"
            >
              {{ submitting() ? 'Granting…' : 'Grant admin' }}
            </button>
            @if (error()) {
              <p class="font-mono text-sm text-red-600 dark:text-red-400" role="alert">{{ error() }}</p>
            }
          </form>
        </app-modal>

        <app-confirm
          [message]="pending()?.message ?? null"
          (confirmed)="runPending()"
          (cancelled)="pending.set(null)"
        />
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
  protected readonly dialogOpen = signal(false);
  protected readonly pending = signal<{ message: string; action: () => void } | null>(null);

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
            this.dialogOpen.set(false);
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

  protected runPending(): void {
    const p = this.pending();
    this.pending.set(null);
    p?.action();
  }

  protected askRevoke(admin: LeagueAdminSummary): void {
    this.pending.set({ message: `Revoke ${admin.email} as a league admin?`, action: () => this.onRevoke(admin.userId) });
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
