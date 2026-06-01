import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { switchMap } from 'rxjs';
import {
  BoardAttendee,
  BoardCourt,
  BoardGamePlayer,
  BoardView,
  GameSide,
  GameType,
  PegboardApi,
} from './pegboard.api';
import { Gender } from './players.api';
import { ModalComponent } from '../../shared/modal.component';
import { ConfirmComponent } from '../../shared/confirm.component';
import { AdminHeaderComponent } from './admin-header.component';

// The set of game types offered in the Fill flow, in display order.
const GAME_TYPES: readonly GameType[] = ['Singles', 'Doubles', 'Mixed', 'Funny'];

// Local view-model: a court paired with its players already split by side, so the template
// never has to filter (no arrow functions allowed in templates).
interface CourtView {
  readonly court: BoardCourt;
  readonly sideA: readonly BoardGamePlayer[];
  readonly sideB: readonly BoardGamePlayer[];
}

// Full-screen, live club-night board. Subscribes to the SSE stream and re-fetches the board on
// every event. Host controls (add court/attendee, fill/start, finish/cancel, close) are rendered
// unconditionally; the board is host-facing and the API returns 403 to non-hosts, surfaced as a
// non-blocking notice rather than gating reads.
@Component({
  selector: 'app-pegboard-board-page',
  imports: [ReactiveFormsModule, ModalComponent, ConfirmComponent, AdminHeaderComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50 dark:bg-slate-950">
      <app-admin-header />

      <main class="mx-auto w-full max-w-[120rem] px-4 py-6 sm:px-6">
        @if (board(); as b) {
          <header class="flex flex-wrap items-end justify-between gap-4 border-b-2 border-slate-900 pb-4 dark:border-amber-400">
            <div>
              <p class="font-mono text-xs uppercase tracking-[0.2em] text-slate-500 dark:text-slate-400">
                Club night · live board
              </p>
              <h1 class="mt-1 font-mono text-3xl font-semibold text-slate-900 dark:text-slate-100">
                {{ b.session.name }}
              </h1>
            </div>
            <div class="flex flex-wrap items-center gap-3">
              <span
                class="inline-flex items-center gap-2 rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs uppercase tracking-wider text-slate-600 dark:border-slate-700 dark:text-slate-300"
              >
                <span class="font-semibold text-slate-900 dark:text-slate-100">{{ playingCount() }}</span> playing
                ·
                <span class="font-semibold text-slate-900 dark:text-slate-100">{{ waiting().length }}</span> waiting
              </span>
              @if (b.session.status === 'Open') {
                <button
                  type="button"
                  (click)="askClose()"
                  class="rounded-md border border-red-300 px-4 py-2 font-mono text-sm font-medium text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-600 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                >
                  Close session
                </button>
              } @else {
                <span class="rounded-md bg-slate-200 px-4 py-2 font-mono text-sm uppercase tracking-wider text-slate-600 dark:bg-slate-800 dark:text-slate-300">
                  Closed
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

          <div class="mt-6 grid gap-6 lg:grid-cols-[1fr_22rem] xl:grid-cols-[1fr_26rem]">
            <!-- Courts grid -->
            <section aria-label="Courts">
              <div class="flex items-center justify-between">
                <h2 class="font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100">
                  Courts
                </h2>
                <button
                  type="button"
                  (click)="courtDialogOpen.set(true)"
                  class="rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs text-slate-700 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
                >
                  ＋ Add court
                </button>
              </div>

              <div class="mt-3 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                @for (cv of courtViews(); track cv.court.id) {
                  <article
                    class="flex flex-col rounded-lg border-2 bg-white p-4 dark:bg-slate-900"
                    [class.border-amber-500]="cv.court.activeGame !== null"
                    [class.dark:border-amber-400]="cv.court.activeGame !== null"
                    [class.border-slate-200]="cv.court.activeGame === null"
                    [class.dark:border-slate-800]="cv.court.activeGame === null"
                  >
                    <div class="flex items-center justify-between">
                      <h3 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
                        {{ cv.court.label }}
                      </h3>
                      @if (cv.court.activeGame; as g) {
                        <span class="rounded bg-amber-200 px-2 py-0.5 font-mono text-xs uppercase tracking-wider text-amber-900 dark:bg-amber-400 dark:text-slate-900">
                          {{ g.type }}
                        </span>
                      } @else {
                        <button
                          type="button"
                          [attr.aria-label]="'Remove ' + cv.court.label"
                          (click)="askRemoveCourt(cv.court)"
                          class="rounded px-2 py-0.5 font-mono text-xs text-slate-400 hover:text-red-600 dark:hover:text-red-400"
                        >
                          ✕
                        </button>
                      }
                    </div>

                    @if (cv.court.activeGame; as g) {
                      <div class="mt-3 grid flex-1 grid-cols-[1fr_auto_1fr] items-stretch gap-2">
                        <div class="rounded-md bg-slate-50 p-3 dark:bg-slate-800/60">
                          <p class="font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400">Side A</p>
                          <ul class="mt-1 space-y-1">
                            @for (p of cv.sideA; track p.attendanceId) {
                              <li class="font-mono text-sm font-medium text-slate-900 dark:text-slate-100">{{ p.displayName }}</li>
                            }
                          </ul>
                        </div>
                        <div class="flex items-center font-mono text-xs font-semibold uppercase text-slate-400">v</div>
                        <div class="rounded-md bg-slate-50 p-3 dark:bg-slate-800/60">
                          <p class="font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400">Side B</p>
                          <ul class="mt-1 space-y-1">
                            @for (p of cv.sideB; track p.attendanceId) {
                              <li class="font-mono text-sm font-medium text-slate-900 dark:text-slate-100">{{ p.displayName }}</li>
                            }
                          </ul>
                        </div>
                      </div>
                      <div class="mt-3 flex gap-2">
                        <button
                          type="button"
                          (click)="openFinish(cv.court)"
                          class="flex-1 rounded-md bg-slate-900 px-3 py-2.5 font-mono text-sm font-medium text-amber-300 hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300 dark:focus:ring-slate-100"
                        >
                          Finish
                        </button>
                        <button
                          type="button"
                          (click)="askCancelGame(cv.court)"
                          class="rounded-md border border-slate-300 px-3 py-2.5 font-mono text-sm text-slate-600 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
                        >
                          Cancel
                        </button>
                      </div>
                    } @else {
                      <div class="mt-3 flex flex-1 flex-col items-center justify-center rounded-md border-2 border-dashed border-slate-200 py-6 dark:border-slate-800">
                        <p class="font-mono text-xs uppercase tracking-wider text-slate-400">Court free</p>
                        <button
                          type="button"
                          (click)="openFill(cv.court)"
                          class="mt-3 rounded-md bg-slate-900 px-5 py-2.5 font-mono text-sm font-medium text-amber-300 hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300 dark:focus:ring-slate-100"
                        >
                          Fill court
                        </button>
                      </div>
                    }
                  </article>
                } @empty {
                  <p class="rounded-md border border-dashed border-slate-300 px-4 py-6 text-center font-mono text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400 sm:col-span-2 xl:col-span-3">
                    No courts yet. Add one to start play.
                  </p>
                }
              </div>
            </section>

            <!-- Waiting queue + status columns -->
            <aside aria-label="Players" class="lg:sticky lg:top-6 lg:self-start">
              <div class="flex items-center justify-between">
                <h2 class="font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100">
                  Waiting <span class="text-slate-400">({{ waiting().length }})</span>
                </h2>
                <button
                  type="button"
                  (click)="attendeeDialogOpen.set(true)"
                  class="rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs text-slate-700 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
                >
                  ＋ Add player
                </button>
              </div>

              <ol class="mt-3 space-y-2">
                @for (a of waiting(); track a.id) {
                  <li class="rounded-md border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900">
                    <div class="flex items-center justify-between gap-2">
                      <span class="font-mono text-sm font-medium text-slate-900 dark:text-slate-100">{{ a.displayName }}</span>
                      <span class="font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400">
                        {{ a.gender }}@if (a.grade !== null) { · G{{ a.grade }} }
                      </span>
                    </div>
                    <div class="mt-2 flex items-center justify-between gap-2">
                      <span class="font-mono text-xs text-slate-500 dark:text-slate-400">
                        {{ a.gamesPlayed }} played · {{ a.gamesWon }} won
                      </span>
                      <div class="flex gap-1">
                        <button
                          type="button"
                          [attr.aria-label]="'Rest ' + a.displayName"
                          (click)="rest(a)"
                          class="rounded border border-slate-300 px-2 py-1 font-mono text-xs text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                        >
                          Rest
                        </button>
                        <button
                          type="button"
                          [attr.aria-label]="a.displayName + ' has left'"
                          (click)="leave(a)"
                          class="rounded border border-slate-300 px-2 py-1 font-mono text-xs text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                        >
                          Leave
                        </button>
                        <button
                          type="button"
                          [attr.aria-label]="'Remove ' + a.displayName"
                          (click)="askRemove(a)"
                          class="rounded border border-red-300 px-2 py-1 font-mono text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                        >
                          ✕
                        </button>
                      </div>
                    </div>
                  </li>
                } @empty {
                  <li class="rounded-md border border-dashed border-slate-300 px-3 py-4 text-center font-mono text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
                    Queue is empty.
                  </li>
                }
              </ol>

              @if (resting().length > 0) {
                <h2 class="mt-6 font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100">
                  Resting <span class="text-slate-400">({{ resting().length }})</span>
                </h2>
                <ol class="mt-3 space-y-2">
                  @for (a of resting(); track a.id) {
                    <li class="flex items-center justify-between gap-2 rounded-md border border-slate-200 bg-white px-3 py-2 dark:border-slate-800 dark:bg-slate-900">
                      <span class="font-mono text-sm text-slate-700 dark:text-slate-300">{{ a.displayName }}</span>
                      <button
                        type="button"
                        [attr.aria-label]="'Return ' + a.displayName + ' to the queue'"
                        (click)="unrest(a)"
                        class="rounded border border-slate-300 px-2 py-1 font-mono text-xs text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                      >
                        Back
                      </button>
                    </li>
                  }
                </ol>
              }
            </aside>
          </div>
        } @else {
          <p class="mt-10 text-center font-mono text-sm text-slate-500 dark:text-slate-400">Loading board…</p>
        }
      </main>
    </div>

    <!-- Add court -->
    <app-modal [open]="courtDialogOpen()" title="Add court" (closed)="courtDialogOpen.set(false)">
      <form [formGroup]="courtForm" (ngSubmit)="onAddCourt()" class="grid gap-3">
        <label class="grid gap-1">
          <span class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Court label</span>
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
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          Add court
        </button>
      </form>
    </app-modal>

    <!-- Add attendee (guest) -->
    <app-modal [open]="attendeeDialogOpen()" title="Add player" (closed)="attendeeDialogOpen.set(false)">
      <form [formGroup]="attendeeForm" (ngSubmit)="onAddAttendee()" class="grid gap-3">
        <label class="grid gap-1">
          <span class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Name</span>
          <input
            type="text"
            formControlName="name"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
            required
          />
        </label>
        <label class="grid gap-1">
          <span class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Gender</span>
          <select
            formControlName="gender"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
          >
            <option value="Male">Male</option>
            <option value="Female">Female</option>
          </select>
        </label>
        <label class="grid gap-1">
          <span class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Grade (optional)</span>
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
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          Add player
        </button>
      </form>
    </app-modal>

    <!-- Fill court -->
    <app-modal [open]="fillCourt() !== null" [title]="fillTitle()" (closed)="closeFill()">
      <div class="grid gap-4">
        <label class="grid gap-1">
          <span class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Game type</span>
          <select
            [value]="fillType()"
            (change)="onFillTypeChange($event)"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
          >
            @for (t of gameTypes; track t) {
              <option [value]="t">{{ t }}</option>
            }
          </select>
        </label>

        <button
          type="button"
          (click)="onSuggest()"
          class="justify-self-start rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
        >
          Suggest a lineup
        </button>

        <fieldset class="grid gap-2">
          <legend class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">
            Tap to assign — A then B
          </legend>
          <ul class="max-h-64 space-y-1 overflow-y-auto">
            @for (a of waiting(); track a.id) {
              <li>
                <button
                  type="button"
                  (click)="cycleSide(a.id)"
                  [attr.aria-pressed]="sideOf(a.id) !== null"
                  class="flex w-full items-center justify-between rounded-md border px-3 py-2 text-left font-mono text-sm"
                  [class.border-slate-200]="sideOf(a.id) === null"
                  [class.dark:border-slate-800]="sideOf(a.id) === null"
                  [class.text-slate-700]="sideOf(a.id) === null"
                  [class.dark:text-slate-300]="sideOf(a.id) === null"
                  [class.border-slate-900]="sideOf(a.id) !== null"
                  [class.dark:border-amber-400]="sideOf(a.id) !== null"
                  [class.bg-slate-50]="sideOf(a.id) !== null"
                  [class.dark:bg-slate-800]="sideOf(a.id) !== null"
                >
                  <span>{{ a.displayName }}</span>
                  @if (sideOf(a.id); as s) {
                    <span class="rounded bg-slate-900 px-2 py-0.5 text-xs text-amber-300 dark:bg-amber-400 dark:text-slate-900">{{ s }}</span>
                  } @else {
                    <span class="text-xs text-slate-400">tap</span>
                  }
                </button>
              </li>
            } @empty {
              <li class="font-mono text-sm text-slate-500 dark:text-slate-400">No one waiting.</li>
            }
          </ul>
        </fieldset>

        <button
          type="button"
          (click)="onStart()"
          [disabled]="!canStart()"
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          Start game
        </button>
      </div>
    </app-modal>

    <!-- Finish game -->
    <app-modal [open]="finishCourt() !== null" title="Finish game" (closed)="closeFinish()">
      <form [formGroup]="finishForm" (ngSubmit)="onFinish()" class="grid gap-3">
        <fieldset class="grid gap-1">
          <legend class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Winner</legend>
          <div class="flex gap-2">
            <label class="flex flex-1 cursor-pointer items-center justify-center gap-2 rounded-md border border-slate-300 px-3 py-3 font-mono text-sm dark:border-slate-700">
              <input type="radio" formControlName="winnerSide" value="A" /> Side A
            </label>
            <label class="flex flex-1 cursor-pointer items-center justify-center gap-2 rounded-md border border-slate-300 px-3 py-3 font-mono text-sm dark:border-slate-700">
              <input type="radio" formControlName="winnerSide" value="B" /> Side B
            </label>
          </div>
        </fieldset>
        <label class="grid gap-1">
          <span class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Score (optional)</span>
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
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
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

  protected readonly gameTypes = GAME_TYPES;

  protected readonly clubId = signal('');
  protected readonly sessionId = signal('');
  protected readonly board = signal<BoardView | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly courtDialogOpen = signal(false);
  protected readonly attendeeDialogOpen = signal(false);

  // Fill flow state.
  protected readonly fillCourt = signal<BoardCourt | null>(null);
  protected readonly fillType = signal<GameType>('Doubles');
  // attendanceId -> assigned side. A plain map signal keeps template lookups arrow-free.
  protected readonly fillAssignments = signal<ReadonlyMap<string, GameSide>>(new Map());

  // Finish flow state.
  protected readonly finishCourt = signal<BoardCourt | null>(null);

  protected readonly pending = signal<{ message: string; action: () => void } | null>(null);

  // Waiting queue, sorted oldest-first (server sorts, but keep it stable here too).
  protected readonly waiting = computed(() =>
    (this.board()?.attendees ?? [])
      .filter((a) => a.status === 'Waiting')
      .slice()
      .sort((x, y) => x.waitingSince.localeCompare(y.waitingSince)),
  );
  protected readonly resting = computed(() =>
    (this.board()?.attendees ?? []).filter((a) => a.status === 'Resting'),
  );
  protected readonly playingCount = computed(
    () => (this.board()?.attendees ?? []).filter((a) => a.status === 'Playing').length,
  );

  // Each court with its active-game players already split by side.
  protected readonly courtViews = computed<CourtView[]>(() =>
    (this.board()?.courts ?? []).map((court) => {
      const players = court.activeGame?.players ?? [];
      return {
        court,
        sideA: players.filter((p) => p.side === 'A'),
        sideB: players.filter((p) => p.side === 'B'),
      };
    }),
  );

  protected readonly fillTitle = computed(() => {
    const c = this.fillCourt();
    return c ? `Fill ${c.label}` : 'Fill court';
  });

  protected readonly courtForm = new FormGroup({
    label: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected readonly attendeeForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    gender: new FormControl<Gender>('Male', { nonNullable: true, validators: [Validators.required] }),
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
        next: (b) => this.board.set(b),
        error: () => this.notice.set('Lost connection to the board.'),
      });
  }

  private refresh(): void {
    this.api.getBoard(this.clubId(), this.sessionId()).subscribe({
      next: (b) => this.board.set(b),
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
    this.fillType.set('Doubles');
    this.fillAssignments.set(new Map());
    this.fillCourt.set(court);
  }

  protected closeFill(): void {
    this.fillCourt.set(null);
    this.fillAssignments.set(new Map());
  }

  protected onFillTypeChange(event: Event): void {
    this.fillType.set((event.target as HTMLSelectElement).value as GameType);
  }

  protected sideOf(attendanceId: string): GameSide | null {
    return this.fillAssignments().get(attendanceId) ?? null;
  }

  // Tap an attendee to cycle unassigned -> A -> B -> unassigned.
  protected cycleSide(attendanceId: string): void {
    const next = new Map(this.fillAssignments());
    const current = next.get(attendanceId);
    if (current === undefined) next.set(attendanceId, 'A');
    else if (current === 'A') next.set(attendanceId, 'B');
    else next.delete(attendanceId);
    this.fillAssignments.set(next);
  }

  protected canStart(): boolean {
    const assignments = this.fillAssignments();
    let a = 0;
    let b = 0;
    for (const side of assignments.values()) {
      if (side === 'A') a++;
      else b++;
    }
    return a > 0 && b > 0;
  }

  protected onSuggest(): void {
    this.notice.set(null);
    this.api.suggest(this.clubId(), this.sessionId(), this.fillType()).subscribe({
      next: (s) => {
        const map = new Map<string, GameSide>();
        for (const id of s.sideA) map.set(id, 'A');
        for (const id of s.sideB) map.set(id, 'B');
        this.fillAssignments.set(map);
      },
      error: (e) => this.onMutationError(e),
    });
  }

  protected onStart(): void {
    const court = this.fillCourt();
    if (!court || !this.canStart()) return;
    const sideA: string[] = [];
    const sideB: string[] = [];
    for (const [id, side] of this.fillAssignments()) {
      if (side === 'A') sideA.push(id);
      else sideB.push(id);
    }
    this.notice.set(null);
    this.api
      .startGame(this.clubId(), this.sessionId(), court.id, this.fillType(), sideA, sideB)
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
      .finishGame(this.clubId(), this.sessionId(), game.id, winnerSide, trimmed === '' ? null : trimmed)
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
      message: 'Close this session? In-progress games end with no result and the board becomes read-only.',
      action: () => this.close(),
    });
  }

  private close(): void {
    this.notice.set(null);
    this.api.closeSession(this.clubId(), this.sessionId()).subscribe({
      next: () =>
        this.router.navigate(['/admin/clubs', this.clubId()], { queryParams: { tab: 'sessions' } }),
      error: (e) => this.onMutationError(e),
    });
  }
}
