import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { BoardCourt, BoardGamePlayer } from '../pegboard.api';

// Presentational: one court, its active game split by side, and the host actions.
// Buttons render only when [live] (an open session the caller may run); a viewer or a
// closed-history board gets the same card with no controls.
@Component({
  selector: 'app-court-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article
      class="flex flex-col rounded-lg border-2 bg-white p-4 dark:bg-slate-900"
      [class.border-amber-500]="court().activeGame !== null"
      [class.dark:border-amber-400]="court().activeGame !== null"
      [class.border-slate-200]="court().activeGame === null"
      [class.dark:border-slate-800]="court().activeGame === null"
    >
      <div class="flex items-center justify-between">
        <h3 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">
          {{ court().label }}
        </h3>
        @if (court().activeGame; as g) {
          <div class="flex items-center gap-2">
            <span
              class="font-mono text-xs tabular-nums text-slate-500 dark:text-slate-400"
              aria-label="Game time"
              >⏱ {{ elapsed() }}</span
            >
            <span
              class="rounded bg-amber-200 px-2 py-0.5 font-mono text-xs uppercase tracking-wider text-amber-900 dark:bg-amber-400 dark:text-slate-900"
            >
              {{ g.type }}
            </span>
          </div>
        } @else if (live()) {
          <button
            type="button"
            [attr.aria-label]="'Remove ' + court().label"
            (click)="remove.emit()"
            class="inline-flex min-h-11 min-w-11 items-center justify-center rounded font-mono text-sm text-slate-400 hover:text-red-600 dark:hover:text-red-400"
          >
            ✕
          </button>
        }
      </div>

      @if (court().activeGame; as g) {
        <div class="mt-3 flex flex-1 items-stretch gap-2">
          <div class="min-w-0 flex-1 rounded-md bg-slate-50 p-3 dark:bg-slate-800/60">
            <p
              class="font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
            >
              Side A
            </p>
            <ul class="mt-1 space-y-1">
              @for (p of sideA(); track p.attendanceId) {
                <li
                  class="break-words font-mono text-sm font-medium text-slate-900 dark:text-slate-100"
                >
                  {{ p.displayName }}
                </li>
              }
            </ul>
          </div>
          <div class="flex items-center font-mono text-xs font-semibold uppercase text-slate-400">
            v
          </div>
          <div class="min-w-0 flex-1 rounded-md bg-slate-50 p-3 dark:bg-slate-800/60">
            <p
              class="font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
            >
              Side B
            </p>
            <ul class="mt-1 space-y-1">
              @for (p of sideB(); track p.attendanceId) {
                <li
                  class="break-words font-mono text-sm font-medium text-slate-900 dark:text-slate-100"
                >
                  {{ p.displayName }}
                </li>
              }
            </ul>
          </div>
        </div>
        @if (live()) {
          <div class="mt-3 flex gap-2">
            <button
              type="button"
              (click)="finish.emit()"
              class="min-h-11 flex-1 rounded-md bg-slate-900 px-3 py-2.5 font-mono text-sm font-medium text-amber-300 hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300 dark:focus:ring-slate-100"
            >
              Finish
            </button>
            <button
              type="button"
              (click)="cancel.emit()"
              class="min-h-11 rounded-md border border-slate-300 px-3 py-2.5 font-mono text-sm text-slate-600 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
            >
              Cancel
            </button>
          </div>
        }
      } @else {
        <div
          class="mt-3 flex flex-1 flex-col items-center justify-center rounded-md border-2 border-dashed border-slate-200 py-6 dark:border-slate-800"
        >
          <p class="font-mono text-xs uppercase tracking-wider text-slate-400">Court free</p>
          @if (live()) {
            <button
              type="button"
              (click)="fill.emit()"
              class="mt-3 min-h-11 rounded-md bg-slate-900 px-5 py-2.5 font-mono text-sm font-medium text-amber-300 hover:bg-slate-800 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300 dark:focus:ring-slate-100"
            >
              Fill court
            </button>
          }
        </div>
      }
    </article>
  `,
})
export class CourtCardComponent {
  readonly court = input.required<BoardCourt>();
  readonly live = input(false);
  // Reference clock (ms) ticked by the parent each second, so the timer advances live.
  readonly now = input(0);

  readonly fill = output<void>();
  readonly finish = output<void>();
  readonly cancel = output<void>();
  readonly remove = output<void>();

  private readonly players = computed<readonly BoardGamePlayer[]>(
    () => this.court().activeGame?.players ?? [],
  );
  protected readonly sideA = computed(() => this.players().filter((p) => p.side === 'A'));
  protected readonly sideB = computed(() => this.players().filter((p) => p.side === 'B'));

  // mm:ss the active game has been running, from its startedAt against the ticking clock.
  protected readonly elapsed = computed(() => {
    const game = this.court().activeGame;
    if (!game) return '0:00';
    const ms = this.now() - Date.parse(game.startedAt);
    const total = Number.isFinite(ms) && ms > 0 ? Math.floor(ms / 1000) : 0;
    const minutes = Math.floor(total / 60);
    const seconds = total % 60;
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  });
}
