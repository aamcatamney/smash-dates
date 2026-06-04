import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ClubsApi, ClubSummary } from './clubs.api';
import { LeaguesApi } from './leagues.api';
import { AuthStore } from '../../core/auth/auth.store';
import { AdminHeaderComponent } from './admin-header.component';
import { ModalComponent } from '../../shared/modal.component';
import { CsvImportComponent } from '../../shared/csv-import.component';
import { ImportResult } from '../../shared/import-result';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'app-clubs-list-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    AdminHeaderComponent,
    ModalComponent,
    CsvImportComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50 dark:bg-slate-950">
      <app-admin-header />

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <div class="flex items-center justify-between">
          <h1 class="font-mono text-2xl font-semibold text-slate-900 dark:text-slate-100">Clubs</h1>
          @if (canCreate()) {
            <div class="flex gap-2">
              <button
                type="button"
                (click)="importOpen.set(true)"
                class="rounded-md border border-slate-300 px-4 py-2 font-mono text-sm text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                Import CSV
              </button>
              <button
                type="button"
                (click)="dialogOpen.set(true)"
                class="rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 hover:bg-slate-800 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300"
              >
                ＋ Create club
              </button>
            </div>
          }
        </div>

        <app-csv-import
          [open]="importOpen()"
          title="Import clubs"
          [columns]="importColumns"
          sample="Thames Valley,TVB,info@thamesvalley.test,admin@thamesvalley.test,"
          [result]="importResult()"
          [busy]="importBusy()"
          (submit)="onImport($event)"
          (closed)="closeImport()"
        />

        <app-modal [open]="dialogOpen()" title="Create club" (closed)="dialogOpen.set(false)">
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
                [attr.aria-describedby]="showError('name') ? 'club-name-error' : null"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                required
              />
              @if (showError('name')) {
                <span
                  id="club-name-error"
                  role="alert"
                  class="font-mono text-xs text-red-600 dark:text-red-400"
                  >Name is required.</span
                >
              }
            </label>
            <label class="grid gap-1">
              <span
                class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                >Short code (3-5 chars)</span
              >
              <input
                type="text"
                formControlName="shortCode"
                [attr.aria-invalid]="showError('shortCode') ? 'true' : null"
                [attr.aria-describedby]="showError('shortCode') ? 'club-shortcode-error' : null"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm uppercase focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                required
              />
              @if (showError('shortCode')) {
                <span
                  id="club-shortcode-error"
                  role="alert"
                  class="font-mono text-xs text-red-600 dark:text-red-400"
                  >3–5 characters required.</span
                >
              }
            </label>
            <label class="grid gap-1">
              <span
                class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                >Contact email</span
              >
              <input
                type="email"
                formControlName="contactEmail"
                [attr.aria-invalid]="showError('contactEmail') ? 'true' : null"
                [attr.aria-describedby]="showError('contactEmail') ? 'club-contact-error' : null"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                required
              />
              @if (showError('contactEmail')) {
                <span
                  id="club-contact-error"
                  role="alert"
                  class="font-mono text-xs text-red-600 dark:text-red-400"
                  >Enter a valid email.</span
                >
              }
            </label>
            <label class="grid gap-1">
              <span
                class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
                >Notes</span
              >
              <input
                type="text"
                formControlName="notes"
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
                [attr.aria-describedby]="showError('firstAdminEmail') ? 'club-admin-error' : null"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:focus:ring-slate-100 dark:bg-slate-800 dark:text-slate-100"
                required
              />
              @if (showError('firstAdminEmail')) {
                <span
                  id="club-admin-error"
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
              {{ submitting() ? 'Creating…' : 'Create club' }}
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
            @for (club of clubs(); track club.id) {
              <li class="px-4 py-3">
                <a
                  [routerLink]="['/admin/clubs', club.id]"
                  class="font-mono text-sm font-medium text-slate-900 hover:underline dark:text-slate-100"
                  >{{ club.shortCode }} · {{ club.name }}</a
                >
                <span class="ml-2 font-mono text-xs text-slate-500 dark:text-slate-400">{{
                  club.contactEmail
                }}</span>
              </li>
            } @empty {
              <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
                No clubs yet.
              </li>
            }
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
  private readonly toast = inject(ToastService);

  protected readonly clubs = signal<ClubSummary[]>([]);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly dialogOpen = signal(false);
  protected readonly loading = signal(true);
  protected readonly canCreate = computed(() => this.auth.isSystemAdmin());

  protected readonly importOpen = signal(false);
  protected readonly importBusy = signal(false);
  protected readonly importResult = signal<ImportResult | null>(null);
  protected readonly importColumns = [
    'name',
    'shortCode',
    'contactEmail',
    'firstAdminEmail',
    'notes',
  ];

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

  // True once a field is both invalid and touched — drives aria-invalid + the inline message.
  protected showError(name: string): boolean {
    const c = this.form.get(name);
    return c !== null && c.invalid && c.touched;
  }

  protected onImport(csv: string): void {
    this.importBusy.set(true);
    this.api.importClubs(csv).subscribe({
      next: (result) => {
        this.importBusy.set(false);
        this.importResult.set(result);
        this.refresh();
      },
      error: () => {
        this.importBusy.set(false);
        this.importResult.set({
          created: 0,
          updated: 0,
          errors: [{ row: 0, message: 'Import failed.' }],
        });
      },
    });
  }

  protected closeImport(): void {
    this.importOpen.set(false);
    this.importResult.set(null);
  }

  private refresh(): void {
    this.api.list().subscribe({
      next: (rows) => {
        this.clubs.set(rows);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load clubs.');
        this.loading.set(false);
      },
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
              this.form.reset({
                name: '',
                shortCode: '',
                contactEmail: '',
                notes: '',
                firstAdminEmail: '',
              });
              this.dialogOpen.set(false);
              this.toast.success(`Club “${name.trim()}” created.`);
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
