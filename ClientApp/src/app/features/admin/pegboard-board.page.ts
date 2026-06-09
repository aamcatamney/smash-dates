import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { switchMap } from 'rxjs';
import {
  BoardAttendee,
  BoardCourt,
  BoardView,
  FillSuggestion,
  GameSide,
  GameType,
  PegboardApi,
} from './pegboard.api';
import { Gender } from './players.api';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmComponent } from '../../shared/confirm.component';
import { AdminHeaderComponent } from './admin-header.component';
import { CourtCardComponent } from './pegboard/court-card.component';
import { WaitingQueueComponent } from './pegboard/waiting-queue.component';
import { FillDialogComponent, StartGamePayload } from './pegboard/fill-dialog.component';

// Full-screen, live club-night board. Subscribes to the SSE stream and re-fetches the board on
// every event. The board read returns `canManage`: host controls render only on a live session
// the caller may run; viewers and closed-history boards get the same layout, read-only.
@Component({
  selector: 'app-pegboard-board-page',
  imports: [
    ReactiveFormsModule,
    ModalComponent,
    ConfirmComponent,
    AdminHeaderComponent,
    CourtCardComponent,
    WaitingQueueComponent,
    FillDialogComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex-1 bg-slate-50 dark:bg-slate-950">
      <app-admin-header />

      <main class="mx-auto w-full max-w-[120rem] px-4 py-6 sm:px-6">
        @if (board(); as b) {
          <header
            class="flex flex-wrap items-end justify-between gap-4 border-b-2 border-slate-900 pb-4 dark:border-amber-400"
          >
            <div>
              <p
                class="font-mono text-xs uppercase tracking-[0.2em] text-slate-500 dark:text-slate-400"
              >
                {{ b.clubName }} · club night
              </p>
              <h1 class="mt-1 font-mono text-3xl font-semibold text-slate-900 dark:text-slate-100">
                {{ b.session.name }}
              </h1>
            </div>
            <div class="flex flex-wrap items-center gap-3">
              @if (isLive()) {
                <span
                  class="inline-flex items-center gap-2 rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs uppercase tracking-wider text-slate-600 dark:border-slate-700 dark:text-slate-300"
                >
                  <span class="font-semibold text-slate-900 dark:text-slate-100">{{
                    playingCount()
                  }}</span>
                  playing ·
                  <span class="font-semibold text-slate-900 dark:text-slate-100">{{
                    waitingCount()
                  }}</span>
                  waiting
                </span>
                <button
                  type="button"
                  (click)="askClose()"
                  class="min-h-11 rounded-md border border-red-300 px-4 py-2 font-mono text-sm font-medium text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-600 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                >
                  Close session
                </button>
              } @else if (b.session.status === 'Closed') {
                <span
                  class="rounded-md bg-slate-200 px-4 py-2 font-mono text-sm uppercase tracking-wider text-slate-600 dark:bg-slate-800 dark:text-slate-300"
                >
                  Closed · history
                </span>
              } @else {
                <span
                  class="rounded-md border border-slate-300 px-4 py-2 font-mono text-sm uppercase tracking-wider text-slate-500 dark:border-slate-700 dark:text-slate-400"
                >
                  Viewing · read-only
                </span>
              }
            </div>
          </header>

          @if (notice(); as n) {
            <p
              role="alert"
              class="mt-4 rounded-md border-l-4 border-amber-500 bg-amber-50 px-4 py-3 font-mono text-sm text-amber-900 dark:border-amber-400 dark:bg-amber-950/60 dark:text-amber-200"
            >
              {{ n }}
            </p>
          }

          <!-- Phone/narrow: one pane at a time via tabs. At lg+ both panes show side-by-side
               and this control is hidden (see ADR 0006). -->
          <div
            class="mt-6 flex gap-1 rounded-lg border-2 border-slate-900 bg-slate-100 p-1 dark:border-amber-400 dark:bg-slate-900 lg:hidden"
          >
            <button
              type="button"
              (click)="tab.set('courts')"
              [attr.aria-pressed]="tab() === 'courts'"
              class="min-h-11 flex-1 rounded-md px-3 py-2 font-mono text-sm font-semibold uppercase tracking-wider"
              [class.bg-slate-900]="tab() === 'courts'"
              [class.text-amber-300]="tab() === 'courts'"
              [class.dark:bg-amber-400]="tab() === 'courts'"
              [class.dark:text-slate-900]="tab() === 'courts'"
              [class.text-slate-500]="tab() !== 'courts'"
            >
              Courts <span class="text-xs">({{ freeCourtCount() }} free)</span>
            </button>
            <button
              type="button"
              (click)="tab.set('waiting')"
              [attr.aria-pressed]="tab() === 'waiting'"
              class="min-h-11 flex-1 rounded-md px-3 py-2 font-mono text-sm font-semibold uppercase tracking-wider"
              [class.bg-slate-900]="tab() === 'waiting'"
              [class.text-amber-300]="tab() === 'waiting'"
              [class.dark:bg-amber-400]="tab() === 'waiting'"
              [class.dark:text-slate-900]="tab() === 'waiting'"
              [class.text-slate-500]="tab() !== 'waiting'"
            >
              {{ isLive() ? 'Waiting' : 'Players' }}
              <span class="text-xs">({{ isLive() ? waitingCount() : attendeeCount() }})</span>
            </button>
          </div>

          <div class="mt-4 grid gap-6 lg:mt-6 lg:grid-cols-[1fr_22rem] xl:grid-cols-[1fr_26rem]">
            <!-- Courts grid -->
            <section aria-label="Courts" class="lg:block" [class.hidden]="tab() !== 'courts'">
              <div class="flex items-center justify-between">
                <h2
                  class="font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100"
                >
                  Courts
                </h2>
                @if (isLive()) {
                  <button
                    type="button"
                    (click)="courtDialogOpen.set(true)"
                    class="inline-flex min-h-11 items-center rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs text-slate-700 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
                  >
                    ＋ Add court
                  </button>
                }
              </div>

              <div class="mt-3 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                @for (c of b.courts; track c.id) {
                  <app-court-card
                    [court]="c"
                    [live]="isLive()"
                    (fill)="openFill(c)"
                    (finish)="openFinish(c)"
                    (cancel)="askCancelGame(c)"
                    (remove)="askRemoveCourt(c)"
                  />
                } @empty {
                  <p
                    class="rounded-md border border-dashed border-slate-300 px-4 py-6 text-center font-mono text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400 sm:col-span-2 xl:col-span-3"
                  >
                    No courts yet.
                    @if (isLive()) {
                      Add one to start play.
                    }
                  </p>
                }
              </div>
            </section>

            <!-- Players -->
            <aside
              aria-label="Players"
              class="lg:sticky lg:top-6 lg:block lg:self-start"
              [class.hidden]="tab() !== 'waiting'"
            >
              <app-waiting-queue
                [attendees]="b.attendees"
                [live]="isLive()"
                [now]="now()"
                (addPlayer)="attendeeDialogOpen.set(true)"
                (rest)="rest($event)"
                (leave)="leave($event)"
                (unrest)="unrest($event)"
                (remove)="askRemove($event)"
              />
            </aside>
          </div>
        } @else {
          <p class="mt-10 text-center font-mono text-sm text-slate-500 dark:text-slate-400">
            Loading board…
          </p>
        }
      </main>
    </div>

    <!-- Add court -->
    <app-modal [open]="courtDialogOpen()" title="Add court" (closed)="courtDialogOpen.set(false)">
      <form [formGroup]="courtForm" (ngSubmit)="onAddCourt()" class="grid gap-3">
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Court label</span
          >
          <input
            type="text"
            formControlName="label"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
            required
          />
        </label>
        <button
          type="submit"
          [disabled]="courtForm.invalid"
          class="min-h-11 justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          Add court
        </button>
      </form>
    </app-modal>

    <!-- Add attendee (guest) -->
    <app-modal
      [open]="attendeeDialogOpen()"
      title="Add player"
      (closed)="attendeeDialogOpen.set(false)"
    >
      <form [formGroup]="attendeeForm" (ngSubmit)="onAddAttendee()" class="grid gap-3">
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Name</span
          >
          <input
            type="text"
            formControlName="name"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
            required
          />
        </label>
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Gender</span
          >
          <select
            formControlName="gender"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
          >
            <option value="Male">Male</option>
            <option value="Female">Female</option>
          </select>
        </label>
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Grade (optional)</span
          >
          <select
            formControlName="grade"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
          >
            <option value="">—</option>
            <option value="1">1</option>
            <option value="2">2</option>
            <option value="3">3</option>
            <option value="4">4</option>
            <option value="5">5</option>
          </select>
        </label>
        <button
          type="submit"
          [disabled]="attendeeForm.invalid"
          class="min-h-11 justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          Add player
        </button>
      </form>
    </app-modal>

    <!-- Fill court -->
    <app-fill-dialog
      [open]="fillCourt() !== null"
      [courtLabel]="fillCourt()?.label ?? ''"
      [waiting]="waiting()"
      [suggestion]="fillSuggestion()"
      (suggest)="onSuggest($event)"
      (autoFill)="onAutoFill($event)"
      (start)="onStart($event)"
      (closed)="closeFill()"
    />

    <!-- Finish game -->
    <app-modal [open]="finishCourt() !== null" title="Finish game" (closed)="closeFinish()">
      <form [formGroup]="finishForm" (ngSubmit)="onFinish()" class="grid gap-3">
        <fieldset class="grid gap-1">
          <legend
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
          >
            Winner
          </legend>
          <div class="flex gap-2">
            <label
              class="flex min-h-11 flex-1 cursor-pointer items-center justify-center gap-2 rounded-md border border-slate-300 px-3 py-3 font-mono text-sm dark:border-slate-700"
            >
              <input type="radio" formControlName="winnerSide" value="A" /> Side A
            </label>
            <label
              class="flex min-h-11 flex-1 cursor-pointer items-center justify-center gap-2 rounded-md border border-slate-300 px-3 py-3 font-mono text-sm dark:border-slate-700"
            >
              <input type="radio" formControlName="winnerSide" value="B" /> Side B
            </label>
          </div>
        </fieldset>
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Score (optional)</span
          >
          <input
            type="text"
            formControlName="score"
            placeholder="21-15"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
          />
        </label>
        <button
          type="submit"
          [disabled]="finishForm.invalid"
          class="min-h-11 justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          Record result
        </button>
      </form>
    </app-modal>

    <app-confirm
      [message]="pending()?.message ?? null"
      (confirmed)="runPending()"
      (cancelled)="pending.set(null)"
    />
  `,
})
export default class PegboardBoardPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(PegboardApi);

  protected readonly clubId = signal('');
  protected readonly sessionId = signal('');
  protected readonly board = signal<BoardView | null>(null);
  protected readonly notice = signal<string | null>(null);
  // Reference clock captured each time the board loads — drives wait-time display.
  protected readonly now = signal(0);

  protected readonly courtDialogOpen = signal(false);
  protected readonly attendeeDialogOpen = signal(false);

  // Phone/narrow layout: which pane the tabs show. Ignored at lg+ (both panes render).
  protected readonly tab = signal<'courts' | 'waiting'>('courts');

  // Fill flow state. The dialog owns the assignment UI; the page owns the court + suggestion.
  protected readonly fillCourt = signal<BoardCourt | null>(null);
  protected readonly fillSuggestion = signal<FillSuggestion | null>(null);

  // Finish flow state.
  protected readonly finishCourt = signal<BoardCourt | null>(null);

  protected readonly pending = signal<{ message: string; action: () => void } | null>(null);

  // The caller may run host controls only on an open session they manage. Viewers and
  // closed-history boards collapse to the same read-only mode.
  protected readonly canManage = computed(() => this.board()?.canManage ?? false);
  protected readonly isLive = computed(
    () => this.canManage() && this.board()?.session.status === 'Open',
  );

  private readonly attendees = computed(() => this.board()?.attendees ?? []);
  protected readonly waiting = computed(() =>
    this.attendees()
      .filter((a) => a.status === 'Waiting')
      .slice()
      .sort((x, y) => x.waitingSince.localeCompare(y.waitingSince)),
  );
  protected readonly waitingCount = computed(() => this.waiting().length);
  protected readonly attendeeCount = computed(() => this.attendees().length);
  protected readonly playingCount = computed(
    () => this.attendees().filter((a) => a.status === 'Playing').length,
  );
  protected readonly freeCourtCount = computed(
    () => this.board()?.courts.filter((c) => c.activeGame === null).length ?? 0,
  );

  protected readonly courtForm = new FormGroup({
    label: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly attendeeForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    gender: new FormControl<Gender>('Male', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    grade: new FormControl('', { nonNullable: true }),
  });

  protected readonly finishForm = new FormGroup({
    winnerSide: new FormControl<GameSide | null>(null, { validators: [Validators.required] }),
    score: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    this.route.paramMap
      .pipe(
        switchMap((p) => {
          this.clubId.set(p.get('id') ?? '');
          this.sessionId.set(p.get('sessionId') ?? '');
          // Re-fetch the board on connect and on every board-changed event.
          return this.api.stream(this.clubId(), this.sessionId());
        }),
        switchMap(() => this.api.getBoard(this.clubId(), this.sessionId())),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: (b) => this.setBoard(b),
        error: () => this.notice.set('Lost connection to the board.'),
      });
  }

  private setBoard(b: BoardView): void {
    this.now.set(Date.now());
    this.board.set(b);
  }

  private refresh(): void {
    this.api.getBoard(this.clubId(), this.sessionId()).subscribe({
      next: (b) => this.setBoard(b),
    });
  }

  // The board is host-facing; a 403 means the caller is a viewer. Surface, don't throw.
  private onMutationError(err: { status?: number; error?: { title?: string } }): void {
    if (err?.status === 403) {
      this.notice.set('You do not have permission to run this session.');
    } else {
      this.notice.set(err?.error?.title ?? 'That action failed.');
    }
  }

  protected runPending(): void {
    const p = this.pending();
    this.pending.set(null);
    p?.action();
  }

  // ---- Courts ----
  protected onAddCourt(): void {
    const label = this.courtForm.getRawValue().label.trim();
    if (!label) return;
    this.notice.set(null);
    this.api.addCourt(this.clubId(), this.sessionId(), label).subscribe({
      next: () => {
        this.courtForm.reset({ label: '' });
        this.courtDialogOpen.set(false);
        this.refresh();
      },
      error: (e) => this.onMutationError(e),
    });
  }

  protected askRemoveCourt(court: BoardCourt): void {
    this.pending.set({
      message: `Remove ${court.label}?`,
      action: () => this.removeCourt(court),
    });
  }

  private removeCourt(court: BoardCourt): void {
    this.notice.set(null);
    this.api.removeCourt(this.clubId(), this.sessionId(), court.id).subscribe({
      next: () => this.refresh(),
      error: (e) => this.onMutationError(e),
    });
  }

  // ---- Attendees ----
  protected onAddAttendee(): void {
    const { name, gender, grade } = this.attendeeForm.getRawValue();
    const trimmed = name.trim();
    if (!trimmed) return;
    const gradeValue = grade ? Number(grade) : null;
    this.notice.set(null);
    this.api.addGuest(this.clubId(), this.sessionId(), trimmed, gender, gradeValue).subscribe({
      next: () => {
        this.attendeeForm.reset({ name: '', gender: 'Male', grade: '' });
        this.attendeeDialogOpen.set(false);
        this.refresh();
      },
      error: (e) => this.onMutationError(e),
    });
  }

  protected rest(a: BoardAttendee): void {
    this.setStatus(a, 'Resting');
  }

  protected unrest(a: BoardAttendee): void {
    this.setStatus(a, 'Waiting');
  }

  protected leave(a: BoardAttendee): void {
    this.setStatus(a, 'Left');
  }

  private setStatus(a: BoardAttendee, status: 'Waiting' | 'Resting' | 'Left'): void {
    this.notice.set(null);
    this.api.setAttendanceStatus(this.clubId(), this.sessionId(), a.id, status).subscribe({
      next: () => this.refresh(),
      error: (e) => this.onMutationError(e),
    });
  }

  protected askRemove(a: BoardAttendee): void {
    this.pending.set({
      message: `Remove ${a.displayName} from the board?`,
      action: () => this.remove(a),
    });
  }

  private remove(a: BoardAttendee): void {
    this.notice.set(null);
    this.api.removeAttendance(this.clubId(), this.sessionId(), a.id).subscribe({
      next: () => this.refresh(),
      error: (e) => this.onMutationError(e),
    });
  }

  // ---- Fill flow ----
  protected openFill(court: BoardCourt): void {
    this.fillSuggestion.set(null);
    this.fillCourt.set(court);
  }

  protected closeFill(): void {
    this.fillCourt.set(null);
    this.fillSuggestion.set(null);
  }

  protected onSuggest(type: GameType): void {
    this.notice.set(null);
    this.api.suggest(this.clubId(), this.sessionId(), type).subscribe({
      next: (s) => this.fillSuggestion.set(s),
      error: (e) => this.onMutationError(e),
    });
  }

  // Auto-rotate: the board picks the lineup and starts the game in one step.
  protected onAutoFill(type: GameType): void {
    const court = this.fillCourt();
    if (!court) return;
    this.notice.set(null);
    this.api.autoFill(this.clubId(), this.sessionId(), court.id, type).subscribe({
      next: () => {
        this.closeFill();
        this.refresh();
      },
      error: (e) => this.onMutationError(e),
    });
  }

  protected onStart(payload: StartGamePayload): void {
    const court = this.fillCourt();
    if (!court) return;
    this.notice.set(null);
    this.api
      .startGame(
        this.clubId(),
        this.sessionId(),
        court.id,
        payload.type,
        payload.sideA,
        payload.sideB,
      )
      .subscribe({
        next: (r) => {
          if (r.makeupWarning) {
            this.notice.set('Started, but the makeup is unusual for this game type.');
          }
          this.closeFill();
          this.refresh();
        },
        error: (e) => this.onMutationError(e),
      });
  }

  // ---- Finish / cancel ----
  protected openFinish(court: BoardCourt): void {
    this.finishForm.reset({ winnerSide: null, score: '' });
    this.finishCourt.set(court);
  }

  protected closeFinish(): void {
    this.finishCourt.set(null);
  }

  protected onFinish(): void {
    const court = this.finishCourt();
    const game = court?.activeGame;
    if (!court || !game) return;
    const { winnerSide, score } = this.finishForm.getRawValue();
    if (!winnerSide) return;
    const trimmed = score.trim();
    this.notice.set(null);
    this.api
      .finishGame(
        this.clubId(),
        this.sessionId(),
        game.id,
        winnerSide,
        trimmed === '' ? null : trimmed,
      )
      .subscribe({
        next: () => {
          this.closeFinish();
          this.refresh();
        },
        error: (e) => this.onMutationError(e),
      });
  }

  protected askCancelGame(court: BoardCourt): void {
    if (!court.activeGame) return;
    this.pending.set({
      message: `Cancel the game on ${court.label} with no result?`,
      action: () => this.cancelGame(court),
    });
  }

  private cancelGame(court: BoardCourt): void {
    const game = court.activeGame;
    if (!game) return;
    this.notice.set(null);
    this.api.cancelGame(this.clubId(), this.sessionId(), game.id).subscribe({
      next: () => this.refresh(),
      error: (e) => this.onMutationError(e),
    });
  }

  // ---- Close session ----
  protected askClose(): void {
    this.pending.set({
      message:
        'Close this session? In-progress games end with no result and the board becomes read-only.',
      action: () => this.close(),
    });
  }

  private close(): void {
    this.notice.set(null);
    this.api.closeSession(this.clubId(), this.sessionId()).subscribe({
      next: () =>
        this.router.navigate(['/clubs', this.clubId()], { queryParams: { tab: 'sessions' } }),
      error: (e) => this.onMutationError(e),
    });
  }
}
