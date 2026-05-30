import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ClubsApi, ClubSummary } from './clubs.api';
import { LeaguesApi } from './leagues.api';
import { AuthStore } from '../../core/auth/auth.store';
import { AdminHeaderComponent } from './admin-header.component';
import { ModalComponent } from '../../shared/modal.component';

@Component({
  selector: 'app-clubs-list-page',
  imports: [ReactiveFormsModule, RouterLink, AdminHeaderComponent, ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <div class="flex items-center justify-between">
          <h1 class="font-mono text-2xl font-semibold text-slate-900">Clubs</h1>
          @if (canCreate()) {
            <button
              type="button"
              (click)="dialogOpen.set(true)"
              class="rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 hover:bg-slate-800"
            >
              ＋ Create club
            </button>
          }
        </div>

        <app-modal [open]="dialogOpen()" title="Create club" (closed)="dialogOpen.set(false)">
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
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Short code (3-5 chars)</span>
              <input
                type="text"
                formControlName="shortCode"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm uppercase focus:outline-none focus:ring-2 focus:ring-slate-900"
                required
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Contact email</span>
              <input
                type="email"
                formControlName="contactEmail"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
                required
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Notes</span>
              <input
                type="text"
                formControlName="notes"
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
              {{ submitting() ? 'Creating…' : 'Create club' }}
            </button>
            @if (error()) {
              <p class="font-mono text-sm text-red-600" role="alert">{{ error() }}</p>
            }
          </form>
        </app-modal>

        <ul class="mt-8 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (club of clubs(); track club.id) {
            <li class="px-4 py-3">
              <a
                [routerLink]="['/admin/clubs', club.id]"
                class="font-mono text-sm font-medium text-slate-900 hover:underline"
                >{{ club.shortCode }} · {{ club.name }}</a
              >
              <span class="ml-2 font-mono text-xs text-slate-500">{{ club.contactEmail }}</span>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No clubs yet.</li>
          }
        </ul>
      </main>
    </div>
  `,
})
export default class ClubsListPage {
  private readonly api = inject(ClubsApi);
  private readonly leagues = inject(LeaguesApi);
  private readonly auth = inject(AuthStore);

  protected readonly clubs = signal<ClubSummary[]>([]);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly dialogOpen = signal(false);
  protected readonly canCreate = computed(() => this.auth.isSystemAdmin());

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    shortCode: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3), Validators.maxLength(5)],
    }),
    contactEmail: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    notes: new FormControl('', { nonNullable: true }),
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
      next: (rows) => this.clubs.set(rows),
      error: () => this.error.set('Failed to load clubs.'),
    });
  }

  protected onCreate(): void {
    const { name, shortCode, contactEmail, notes, firstAdminEmail } = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);

    this.leagues.lookupUser(firstAdminEmail.trim()).subscribe({
      next: (user) => {
        const trimmedNotes = notes.trim();
        this.api
          .create({
            name: name.trim(),
            shortCode: shortCode.trim().toUpperCase(),
            contactEmail: contactEmail.trim(),
            notes: trimmedNotes ? trimmedNotes : null,
            firstClubAdminUserId: user.id,
          })
          .subscribe({
            next: () => {
              this.submitting.set(false);
              this.form.reset({ name: '', shortCode: '', contactEmail: '', notes: '', firstAdminEmail: '' });
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
        this.error.set('No registered user with that email.');
      },
    });
  }
}
