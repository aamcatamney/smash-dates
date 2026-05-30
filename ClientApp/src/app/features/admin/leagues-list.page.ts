import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LeaguesApi, LeagueSummary } from './leagues.api';
import { AuthStore } from '../../core/auth/auth.store';
import { AdminHeaderComponent } from './admin-header.component';
import { ModalComponent } from '../../shared/modal.component';

@Component({
  selector: 'app-leagues-list-page',
  imports: [ReactiveFormsModule, RouterLink, AdminHeaderComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <div class="flex items-center justify-between">
          <h1 class="font-mono text-2xl font-semibold text-slate-900">Leagues</h1>
          @if (canCreate()) {
            <button
              type="button"
              (click)="dialogOpen.set(true)"
              class="rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 hover:bg-slate-800"
            >
              ＋ Create league
            </button>
          }
        </div>

        <app-modal [open]="dialogOpen()" title="Create league" (closed)="dialogOpen.set(false)">
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
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Description</span>
            <input
              type="text"
              formControlName="description"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
            />
          </label>
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">First admin email</span>
            <input
              type="email"
              formControlName="firstAdminEmail"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              required
            />
          </label>
          <button
            type="submit"
            [disabled]="submitting() || form.invalid"
            class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ submitting() ? 'Creating…' : 'Create league' }}
          </button>
          @if (error()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ error() }}</p>
          }
        </form>
        </app-modal>

        <ul class="mt-8 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @if (loading()) {
            <li class="px-4 py-3 font-mono text-sm text-slate-400">Loading…</li>
          } @else {
            @for (league of leagues(); track league.id) {
              <li class="px-4 py-3">
                <a
                  [routerLink]="['/admin/leagues', league.id]"
                  class="font-mono text-sm font-medium text-slate-900 hover:underline"
                  >{{ league.name }}</a
                >
                @if (league.description) {
                  <span class="ml-2 font-mono text-sm text-slate-500">— {{ league.description }}</span>
                }
              </li>
            } @empty {
              <li class="px-4 py-3 font-mono text-sm text-slate-500">No leagues yet.</li>
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
