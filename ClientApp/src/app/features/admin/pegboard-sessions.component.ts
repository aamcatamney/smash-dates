import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ModalComponent } from '../../shared/modal.component';
import { PegboardApi, ScheduleInput, SessionSummary } from './pegboard.api';
import { ClubsApi, VenueSummary } from './clubs.api';
import { AuthStore } from '../../core/auth/auth.store';

// "Sessions" tab on the club page: lists this club's pegboard sessions grouped into Upcoming
// (Scheduled), Live (Open) and Past (Closed). A runner can open one for tonight, schedule one
// ahead of time (optional time/duration/venue), open a scheduled one when the night begins, or
// edit/drop a scheduled one. Opening navigates to the full-screen board; a 409 (the club already
// has an open session) surfaces a notice and refreshes the list.
@Component({
  selector: 'app-pegboard-sessions',
  imports: [ReactiveFormsModule, RouterLink, ModalComponent, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mt-10 flex flex-wrap items-center justify-between gap-2">
      <h2 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">Sessions</h2>
      @if (canRun()) {
        <div class="flex gap-2">
          <button
            type="button"
            (click)="openScheduleDialog()"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            ＋ Schedule session
          </button>
          <button
            type="button"
            (click)="openNowDialog()"
            class="rounded-md bg-slate-900 px-3 py-1 font-mono text-xs font-medium text-amber-300 dark:bg-amber-400 dark:text-slate-900"
          >
            ＋ Open session now
          </button>
        </div>
      }
    </div>

    @if (notice()) {
      <p
        class="mt-3 rounded-md border border-amber-300 bg-amber-50 px-3 py-2 font-mono text-xs text-amber-800 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-300"
        role="status"
      >
        {{ notice() }}
      </p>
    }

    <!-- Live -->
    @if (live(); as l) {
      <h3
        class="mt-6 font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
      >
        Live now
      </h3>
      <ul
        class="mt-2 divide-y divide-slate-200 rounded-md border border-emerald-300 bg-white dark:divide-slate-800 dark:border-emerald-800 dark:bg-slate-900"
      >
        <li class="px-4 py-3 font-mono text-sm">
          <a
            [routerLink]="['/clubs', clubId(), 'pegboard', l.id]"
            class="flex flex-wrap items-center gap-x-3 gap-y-1 hover:underline"
          >
            <span class="font-medium text-slate-900 dark:text-slate-100">{{ l.name }}</span>
            <span
              class="inline-block rounded bg-emerald-100 px-2 py-0.5 text-xs text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300"
              >Open</span
            >
            <span class="text-slate-500 dark:text-slate-400"
              >opened {{ l.openedAt | date: 'medium' }}</span
            >
          </a>
        </li>
      </ul>
    }

    <!-- Upcoming -->
    <h3 class="mt-6 font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400">
      Upcoming
    </h3>
    <ul
      class="mt-2 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900"
    >
      @for (s of upcoming(); track s.id) {
        <li class="flex flex-wrap items-center justify-between gap-2 px-4 py-3 font-mono text-sm">
          <div class="flex flex-col gap-0.5">
            <span class="font-medium text-slate-900 dark:text-slate-100">{{ s.name }}</span>
            <span class="text-slate-500 dark:text-slate-400">
              {{ s.scheduledDate | date: 'fullDate'
              }}{{ s.startTime ? ' · ' + clock(s.startTime) : ''
              }}{{ s.durationMinutes ? ' · ' + s.durationMinutes + ' min' : ''
              }}{{ s.venueName ? ' · ' + s.venueName : '' }}
            </span>
          </div>
          @if (canRun()) {
            @if (confirmDeleteId() === s.id) {
              <div class="flex items-center gap-2">
                <span class="text-xs text-red-600 dark:text-red-400">Delete?</span>
                <button
                  type="button"
                  (click)="onDelete(s)"
                  class="rounded border border-red-300 px-2 py-0.5 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                >
                  Confirm
                </button>
                <button
                  type="button"
                  (click)="confirmDeleteId.set(null)"
                  class="rounded border border-slate-300 px-2 py-0.5 text-xs text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                >
                  Cancel
                </button>
              </div>
            } @else {
              <div class="flex items-center gap-2">
                <button
                  type="button"
                  (click)="onOpenNow(s)"
                  [disabled]="busy()"
                  class="rounded bg-slate-900 px-2 py-0.5 text-xs font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
                >
                  Open now
                </button>
                <button
                  type="button"
                  (click)="openScheduleDialog(s)"
                  class="rounded border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                >
                  Edit
                </button>
                <button
                  type="button"
                  (click)="confirmDeleteId.set(s.id)"
                  class="rounded border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                >
                  Delete
                </button>
              </div>
            }
          }
        </li>
      } @empty {
        <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
          No upcoming sessions.
        </li>
      }
    </ul>

    <!-- Past -->
    @if (past().length) {
      <h3
        class="mt-6 font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
      >
        Past
      </h3>
      <ul
        class="mt-2 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900"
      >
        @for (s of past(); track s.id) {
          <li class="px-4 py-3 font-mono text-sm">
            <a
              [routerLink]="['/clubs', clubId(), 'pegboard', s.id]"
              class="flex flex-wrap items-center gap-x-3 gap-y-1 hover:underline"
            >
              <span class="font-medium text-slate-900 dark:text-slate-100">{{ s.name }}</span>
              <span
                class="inline-block rounded bg-slate-200 px-2 py-0.5 text-xs text-slate-700 dark:bg-slate-700 dark:text-slate-200"
                >Closed</span
              >
              <span class="text-slate-500 dark:text-slate-400"
                >opened {{ s.openedAt | date: 'medium' }}</span
              >
            </a>
          </li>
        }
      </ul>
    }

    <!-- Open now -->
    <app-modal
      [open]="nowDialogOpen()"
      title="Open session now"
      (closed)="nowDialogOpen.set(false)"
    >
      <form [formGroup]="nowForm" (ngSubmit)="onOpenNowSubmit()" class="grid gap-3">
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Session name</span
          >
          <input
            type="text"
            formControlName="name"
            placeholder="e.g. Tuesday Club Night"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
            required
          />
        </label>
        <button
          type="submit"
          [disabled]="busy() || nowForm.invalid"
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          {{ busy() ? 'Opening…' : 'Open session' }}
        </button>
        @if (error()) {
          <p class="font-mono text-sm text-red-600 dark:text-red-400" role="alert">{{ error() }}</p>
        }
      </form>
    </app-modal>

    <!-- Schedule / edit -->
    <app-modal
      [open]="scheduleDialogOpen()"
      [title]="editingId() ? 'Edit scheduled session' : 'Schedule a session'"
      (closed)="scheduleDialogOpen.set(false)"
    >
      <form [formGroup]="scheduleForm" (ngSubmit)="onSchedule()" class="grid gap-3">
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Session name</span
          >
          <input
            type="text"
            formControlName="name"
            placeholder="e.g. Tuesday Club Night"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
            required
          />
        </label>
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Date</span
          >
          <input
            type="date"
            formControlName="scheduledDate"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
            required
          />
        </label>
        <div class="grid grid-cols-2 gap-3">
          <label class="grid gap-1">
            <span
              class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
              >Start time (optional)</span
            >
            <input
              type="time"
              formControlName="startTime"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
            />
          </label>
          <label class="grid gap-1">
            <span
              class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
              >Duration min (optional)</span
            >
            <input
              type="number"
              min="1"
              formControlName="durationMinutes"
              placeholder="e.g. 120"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
            />
          </label>
        </div>
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Venue (optional)</span
          >
          <select
            formControlName="venueId"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
          >
            <option value="">— none —</option>
            @for (v of venues(); track v.id) {
              <option [value]="v.id">{{ v.name }}</option>
            }
          </select>
        </label>
        <button
          type="submit"
          [disabled]="busy() || scheduleForm.invalid"
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          {{ busy() ? 'Saving…' : editingId() ? 'Save changes' : 'Schedule session' }}
        </button>
        @if (error()) {
          <p class="font-mono text-sm text-red-600 dark:text-red-400" role="alert">{{ error() }}</p>
        }
      </form>
    </app-modal>
  `,
})
export class PegboardSessionsComponent {
  private readonly api = inject(PegboardApi);
  private readonly clubsApi = inject(ClubsApi);
  private readonly router = inject(Router);
  private readonly store = inject(AuthStore);

  readonly clubId = input.required<string>();
  // Only a club admin or SessionHost may open/run/schedule sessions.
  protected readonly canRun = computed(() => this.store.isSessionRunner(this.clubId()));

  protected readonly sessions = signal<SessionSummary[]>([]);
  protected readonly venues = signal<VenueSummary[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly confirmDeleteId = signal<string | null>(null);

  // Grouped views.
  protected readonly live = computed(
    () => this.sessions().find((s) => s.status === 'Open') ?? null,
  );
  protected readonly upcoming = computed(() =>
    this.sessions().filter((s) => s.status === 'Scheduled'),
  );
  protected readonly past = computed(() => this.sessions().filter((s) => s.status === 'Closed'));

  // Open-now dialog.
  protected readonly nowDialogOpen = signal(false);
  protected readonly nowForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  // Schedule / edit dialog. editingId is null for a new session, set when editing.
  protected readonly scheduleDialogOpen = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly scheduleForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    scheduledDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    startTime: new FormControl('', { nonNullable: true }),
    durationMinutes: new FormControl<number | null>(null),
    venueId: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    effect(() => {
      if (this.clubId()) {
        this.refresh();
        this.clubsApi
          .listVenues(this.clubId())
          .subscribe({ next: (rows) => this.venues.set(rows) });
      }
    });
  }

  // "HH:mm:ss" (or "HH:mm") -> "HH:mm" for display.
  protected clock(time: string): string {
    return time.slice(0, 5);
  }

  private refresh(): void {
    this.api.listSessions(this.clubId()).subscribe({ next: (rows) => this.sessions.set(rows) });
  }

  protected openNowDialog(): void {
    this.error.set(null);
    this.notice.set(null);
    this.nowForm.reset({ name: '' });
    this.nowDialogOpen.set(true);
  }

  protected onOpenNowSubmit(): void {
    const name = this.nowForm.getRawValue().name.trim();
    if (!name) return;
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.openSession(this.clubId(), name).subscribe({
      next: (created) => {
        this.busy.set(false);
        this.nowDialogOpen.set(false);
        this.router.navigate(['/clubs', this.clubId(), 'pegboard', created.id]);
      },
      error: (err: { status?: number; error?: { title?: string } }) => this.handleOpenError(err),
    });
  }

  protected openScheduleDialog(session?: SessionSummary): void {
    this.error.set(null);
    this.notice.set(null);
    this.confirmDeleteId.set(null);
    if (session) {
      this.editingId.set(session.id);
      this.scheduleForm.reset({
        name: session.name,
        scheduledDate: session.scheduledDate ?? '',
        startTime: session.startTime ? session.startTime.slice(0, 5) : '',
        durationMinutes: session.durationMinutes,
        venueId: session.venueId ?? '',
      });
    } else {
      this.editingId.set(null);
      this.scheduleForm.reset({
        name: '',
        scheduledDate: '',
        startTime: '',
        durationMinutes: null,
        venueId: '',
      });
    }
    this.scheduleDialogOpen.set(true);
  }

  protected onSchedule(): void {
    const raw = this.scheduleForm.getRawValue();
    if (!raw.name.trim() || !raw.scheduledDate) return;
    const input: ScheduleInput = {
      name: raw.name.trim(),
      scheduledDate: raw.scheduledDate,
      // Backend expects an ISO time; the time input gives "HH:mm".
      startTime: raw.startTime ? `${raw.startTime}:00` : null,
      durationMinutes: raw.durationMinutes ?? null,
      venueId: raw.venueId || null,
    };
    this.busy.set(true);
    this.error.set(null);
    const editingId = this.editingId();
    const done = () => {
      this.busy.set(false);
      this.scheduleDialogOpen.set(false);
      this.refresh();
    };
    const fail = (err: { error?: { title?: string } }) => {
      this.busy.set(false);
      this.error.set(err?.error?.title ?? 'Could not save the session.');
    };
    if (editingId) {
      this.api
        .updateScheduledSession(this.clubId(), editingId, input)
        .subscribe({ next: done, error: fail });
    } else {
      this.api.scheduleSession(this.clubId(), input).subscribe({ next: done, error: fail });
    }
  }

  protected onOpenNow(session: SessionSummary): void {
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.openScheduledSession(this.clubId(), session.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.router.navigate(['/clubs', this.clubId(), 'pegboard', session.id]);
      },
      error: (err: { status?: number; error?: { title?: string } }) => this.handleOpenError(err),
    });
  }

  protected onDelete(session: SessionSummary): void {
    this.confirmDeleteId.set(null);
    this.api.deleteScheduledSession(this.clubId(), session.id).subscribe({
      next: () => this.refresh(),
      error: (err: { error?: { title?: string } }) =>
        this.notice.set(err?.error?.title ?? 'Could not delete the session.'),
    });
  }

  private handleOpenError(err: { status?: number; error?: { title?: string } }): void {
    this.busy.set(false);
    if (err?.status === 409) {
      this.nowDialogOpen.set(false);
      this.notice.set('This club already has an open session — its link is above.');
      this.refresh();
      return;
    }
    this.error.set(err?.error?.title ?? 'Could not open session.');
  }
}
