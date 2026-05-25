import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LeaguesApi, LeagueSummary } from './leagues.api';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-leagues-list-page',
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
        <h1 class="font-mono text-2xl font-semibold text-slate-900">Leagues</h1>

        @if (canCreate()) {
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
        }

        <ul class="mt-8 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
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
      next: (rows) => this.leagues.set(rows),
      error: () => this.error.set('Failed to load leagues.'),
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
