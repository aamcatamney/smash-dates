import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ModalComponent } from '../../shared/modal.component';
import { PegboardApi, SessionSummary } from './pegboard.api';

// "Sessions" tab on the club page: lists this club's pegboard sessions (current + past) and
// opens a new one. Each row links to the full-screen board route. Opening a session navigates
// straight to its board; if the club already has an open session (409) we surface a notice and
// refresh the list so the existing open session's row/link is visible.
@Component({
  selector: 'app-pegboard-sessions',
  imports: [ReactiveFormsModule, RouterLink, ModalComponent, DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mt-10 flex items-center justify-between">
      <h2 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">Sessions</h2>
      <button
        type="button"
        (click)="openDialog()"
        class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
      >
        ＋ Open session
      </button>
    </div>

    @if (notice()) {
      <p class="mt-3 rounded-md border border-amber-300 bg-amber-50 px-3 py-2 font-mono text-xs text-amber-800 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-300" role="status">
        {{ notice() }}
      </p>
    }

    <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
      @for (s of sessions(); track s.id) {
        <li class="px-4 py-3 font-mono text-sm">
          <a
            [routerLink]="['/admin/clubs', clubId(), 'pegboard', s.id]"
            class="flex flex-wrap items-center gap-x-3 gap-y-1 hover:underline"
          >
            <span class="font-medium text-slate-900 dark:text-slate-100">{{ s.name }}</span>
            <span
              class="inline-block rounded px-2 py-0.5 text-xs"
              [class]="
                s.status === 'Open'
                  ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                  : 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200'
              "
              >{{ s.status }}</span
            >
            <span class="text-slate-500 dark:text-slate-400">opened {{ s.openedAt | date: 'medium' }}</span>
          </a>
        </li>
      } @empty {
        <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">No sessions yet.</li>
      }
    </ul>

    <app-modal [open]="dialogOpen()" title="Open session" (closed)="dialogOpen.set(false)">
      <form [formGroup]="form" (ngSubmit)="onOpen()" class="grid gap-3">
        <label class="grid gap-1">
          <span class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Session name</span>
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
          [disabled]="busy() || form.invalid"
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          {{ busy() ? 'Opening…' : 'Open session' }}
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
  private readonly router = inject(Router);

  readonly clubId = input.required<string>();

  protected readonly sessions = signal<SessionSummary[]>([]);
  protected readonly dialogOpen = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    effect(() => {
      if (this.clubId()) this.refresh();
    });
  }

  private refresh(): void {
    this.api.listSessions(this.clubId()).subscribe({ next: (rows) => this.sessions.set(rows) });
  }

  protected openDialog(): void {
    this.error.set(null);
    this.notice.set(null);
    this.form.reset({ name: '' });
    this.dialogOpen.set(true);
  }

  protected onOpen(): void {
    const name = this.form.getRawValue().name.trim();
    if (!name) return;
    this.busy.set(true);
    this.error.set(null);
    this.notice.set(null);
    this.api.openSession(this.clubId(), name).subscribe({
      next: (created) => {
        this.busy.set(false);
        this.dialogOpen.set(false);
        this.router.navigate(['/admin/clubs', this.clubId(), 'pegboard', created.id]);
      },
      error: (err: { status?: number; error?: { title?: string } }) => {
        this.busy.set(false);
        if (err?.status === 409) {
          this.dialogOpen.set(false);
          this.notice.set('This club already has an open session — its link is below.');
          this.refresh();
          return;
        }
        this.error.set(err?.error?.title ?? 'Could not open session.');
      },
    });
  }
}
