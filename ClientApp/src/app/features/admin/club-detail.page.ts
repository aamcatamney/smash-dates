import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import { ClubAdminSummary, ClubDetail, ClubsApi, MembershipSummary } from './clubs.api';
import { LeaguesApi } from './leagues.api';

@Component({
  selector: 'app-club-detail-page',
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
  protected readonly adminBusy = signal(false);
  protected readonly adminError = signal<string | null>(null);

  protected readonly adminForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
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
}
