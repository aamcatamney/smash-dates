import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LeaguesApi, LeagueSummary } from './leagues.api';
import { AuthStore } from '../../core/auth/auth.store';
import { AdminHeaderComponent } from './admin-header.component';
import { ModalComponent } from '../../shared/modal.component';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-leagues-list-page',
  imports: [ReactiveFormsModule, RouterLink, AdminHeaderComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50 dark:bg-slate-950">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <div class="flex items-center justify-between">
          <h1 class="font-mono text-2xl font-semibold text-slate-900 dark:text-slate-100">
            Leagues
          </h1>
          @if (canCreate()) {
            <button
              type="button"
              (click)="dialogOpen.set(true)"
              class="rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 hover:bg-slate-800 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300"
            >
              ＋ Create league
            </button>
          }
        </div>

        <app-modal [open]="dialogOpen()" title="Create league" (closed)="dialogOpen.set(false)">
          <form [formGroup]="form" (ngSubmit)="onCreate()" class="grid gap-3">
            <label class="grid gap-1">
              <span
                class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                >Name</span
              >
              <input
                type="text"
                formControlName="name"
                [attr.aria-invalid]="showError('name') ? 'true' : null"
                [attr.aria-describedby]="showError('name') ? 'league-name-error' : null"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                required
              />
              @if (showError('name')) {
                <span
                  id="league-name-error"
                  role="alert"
                  class="font-mono text-xs text-red-600 dark:text-red-400"
                  >Name is required.</span
                >
              }
            </label>
            <label class="grid gap-1">
              <span
                class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                >Description</span
              >
              <input
                type="text"
                formControlName="description"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
              />
            </label>
            <label class="grid gap-1">
              <span
                class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                >First admin email</span
              >
              <input
                type="email"
                formControlName="firstAdminEmail"
                [attr.aria-invalid]="showError('firstAdminEmail') ? 'true' : null"
                [attr.aria-describedby]="showError('firstAdminEmail') ? 'league-admin-error' : null"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                required
              />
              @if (showError('firstAdminEmail')) {
                <span
                  id="league-admin-error"
                  role="alert"
                  class="font-mono text-xs text-red-600 dark:text-red-400"
                  >Enter a valid email.</span
                >
              }
            </label>
            <button
              type="submit"
              [disabled]="submitting() || form.invalid"
              class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
            >
              {{ submitting() ? 'Creating…' : 'Create league' }}
            </button>
            @if (error()) {
              <p class="font-mono text-sm text-red-600 dark:text-red-400" role="alert">
                {{ error() }}
              </p>
            }
          </form>
        </app-modal>

        <ul
          class="mt-8 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900"
        >
          @if (loading()) {
            <li class="px-4 py-3 font-mono text-sm text-slate-400 dark:text-slate-500">Loading…</li>
          } @else {
            @for (league of leagues(); track league.id) {
              <li class="px-4 py-3">
                <a
                  [routerLink]="['/admin/leagues', league.id]"
                  class="font-mono text-sm font-medium text-slate-900 hover:underline dark:text-slate-100"
                  >{{ league.name }}</a
                >
                @if (league.description) {
                  <span class="ml-2 font-mono text-sm text-slate-500 dark:text-slate-400"
                    >— {{ league.description }}</span
                  >
                }
                <div
                  class="mt-1 flex flex-wrap items-center gap-2 font-mono text-xs text-slate-500 dark:text-slate-400"
                >
                  <span class="rounded bg-slate-100 px-1.5 py-0.5 dark:bg-slate-800"
                    >{{ league.divisionCount }}
                    {{ league.divisionCount === 1 ? 'division' : 'divisions' }}</span
                  >
                  <span class="rounded bg-slate-100 px-1.5 py-0.5 dark:bg-slate-800"
                    >{{ league.playerCount }}
                    {{ league.playerCount === 1 ? 'player' : 'players' }}</span
                  >
                  @if (league.activeSeasonName) {
                    <span
                      class="rounded bg-emerald-100 px-1.5 py-0.5 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300"
                      >Active: {{ league.activeSeasonName }}</span
                    >
                  } @else {
                    <span class="rounded bg-slate-100 px-1.5 py-0.5 dark:bg-slate-800"
                      >No active season</span
                    >
                  }
                </div>
              </li>
            } @empty {
              <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                No leagues yet.
              </li>
            }
          }
        </ul>
      </main>
    </div>
  `,
})
export default class LeaguesListPage {
  private readonly api = inject(LeaguesApi);
  private readonly auth = inject(AuthStore);
  private readonly toast = inject(ToastService);

  protected readonly leagues = signal<LeagueSummary[]>([]);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly dialogOpen = signal(false);
  protected readonly loading = signal(true);
  protected readonly canCreate = computed(() => this.auth.isSystemAdmin());

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    description: new FormControl('', { nonNullable: true }),
    firstAdminEmail: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
  });

  constructor() {
    this.refresh();
  }

  // True once a field is both invalid and touched — drives aria-invalid + the inline message.
  protected showError(name: string): boolean {
    const c = this.form.get(name);
    return c !== null && c.invalid && c.touched;
  }

  private refresh(): void {
    this.api.list().subscribe({
      next: (rows) => {
        this.leagues.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load leagues.');
        this.loading.set(false);
      },
    });
  }

  protected onCreate(): void {
    const { name, description, firstAdminEmail } = this.form.getRawValue();
    const trimmedName = name.trim();
    if (!trimmedName) return;

    this.submitting.set(true);
    this.error.set(null);
    const trimmedDescription = description.trim();

    this.api.lookupUser(firstAdminEmail.trim()).subscribe({
      next: (user) => {
        this.api
          .create({
            name: trimmedName,
            description: trimmedDescription ? trimmedDescription : null,
            firstLeagueAdminUserId: user.id,
          })
          .subscribe({
            next: () => {
              this.submitting.set(false);
              this.form.reset({ name: '', description: '', firstAdminEmail: '' });
              this.dialogOpen.set(false);
              this.toast.success(`League “${trimmedName}” created.`);
              this.refresh();
            },
            error: (err: { error?: { title?: string } }) => {
              this.submitting.set(false);
              this.error.set(err?.error?.title ?? 'Create failed.');
            },
          });
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('No registered user with that email — they must register first.');
      },
    });
  }
}
