import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { BoardAttendee } from '../pegboard.api';

// Minutes an attendee has been waiting, relative to a reference `now` (ms) captured when the
// board last loaded. Kept pure so the template can call it without an arrow function.
export function waitMinutes(sinceIso: string, nowMs: number): number {
  const since = Date.parse(sinceIso);
  if (Number.isNaN(since)) return 0;
  return Math.max(0, Math.floor((nowMs - since) / 60_000));
}

// Presentational players panel. When [live] it shows the Waiting queue (with Rest/Leave/Remove
// and wait time) plus a Resting section; when not live (viewer or closed history) it shows a
// flat, read-only roster of everyone who attended with their final played/won stats.
@Component({
  selector: 'app-waiting-queue',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (live()) {
      <div class="flex items-center justify-between">
        <h2
          class="font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100"
        >
          Waiting <span class="text-slate-400">({{ waiting().length }})</span>
        </h2>
        <button
          type="button"
          (click)="addPlayer.emit()"
          class="inline-flex min-h-11 items-center rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs text-slate-700 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
        >
          ＋ Add player
        </button>
      </div>

      <ol class="mt-3 space-y-2">
        @for (a of waiting(); track a.id) {
          <li
            class="rounded-md border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900"
          >
            <div class="flex items-center justify-between gap-2">
              <span class="font-mono text-sm font-medium text-slate-900 dark:text-slate-100">{{
                a.displayName
              }}</span>
              <span
                class="font-mono text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400"
              >
                {{ a.gender }}
                @if (a.grade !== null) {
                  · G{{ a.grade }}
                }
              </span>
            </div>
            <div class="mt-2 flex items-center justify-between gap-2">
              <span class="font-mono text-xs text-slate-500 dark:text-slate-400">
                waiting {{ wait(a) }}m · {{ a.gamesPlayed }} played · {{ a.gamesWon }} won
              </span>
              <div class="flex gap-1">
                <button
                  type="button"
                  [attr.aria-label]="'Rest ' + a.displayName"
                  (click)="rest.emit(a)"
                  class="min-h-11 rounded border border-slate-300 px-3 py-1 font-mono text-xs text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                >
                  Rest
                </button>
                <button
                  type="button"
                  [attr.aria-label]="a.displayName + ' has left'"
                  (click)="leave.emit(a)"
                  class="min-h-11 rounded border border-slate-300 px-3 py-1 font-mono text-xs text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                >
                  Leave
                </button>
                <button
                  type="button"
                  [attr.aria-label]="'Remove ' + a.displayName"
                  (click)="remove.emit(a)"
                  class="min-h-11 min-w-11 rounded border border-red-300 px-3 py-1 font-mono text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
                >
                  ✕
                </button>
              </div>
            </div>
          </li>
        } @empty {
          <li
            class="rounded-md border border-dashed border-slate-300 px-3 py-4 text-center font-mono text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400"
          >
            Queue is empty.
          </li>
        }
      </ol>

      @if (resting().length > 0) {
        <h2
          class="mt-6 font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100"
        >
          Resting <span class="text-slate-400">({{ resting().length }})</span>
        </h2>
        <ol class="mt-3 space-y-2">
          @for (a of resting(); track a.id) {
            <li
              class="flex items-center justify-between gap-2 rounded-md border border-slate-200 bg-white px-3 py-2 dark:border-slate-800 dark:bg-slate-900"
            >
              <span class="font-mono text-sm text-slate-700 dark:text-slate-300">{{
                a.displayName
              }}</span>
              <button
                type="button"
                [attr.aria-label]="'Return ' + a.displayName + ' to the queue'"
                (click)="unrest.emit(a)"
                class="min-h-11 rounded border border-slate-300 px-3 py-1 font-mono text-xs text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                Back
              </button>
            </li>
          }
        </ol>
      }
    } @else {
      <h2
        class="font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100"
      >
        Attendees <span class="text-slate-400">({{ roster().length }})</span>
      </h2>
      <ol class="mt-3 space-y-2">
        @for (a of roster(); track a.id) {
          <li
            class="flex items-center justify-between gap-2 rounded-md border border-slate-200 bg-white px-3 py-2 dark:border-slate-800 dark:bg-slate-900"
          >
            <span class="font-mono text-sm text-slate-700 dark:text-slate-300">{{
              a.displayName
            }}</span>
            <span class="font-mono text-xs text-slate-500 dark:text-slate-400">
              {{ a.gamesPlayed }} played · {{ a.gamesWon }} won
            </span>
          </li>
        } @empty {
          <li
            class="rounded-md border border-dashed border-slate-300 px-3 py-4 text-center font-mono text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400"
          >
            No one attended.
          </li>
        }
      </ol>
    }
  `,
})
export class WaitingQueueComponent {
  readonly attendees = input.required<readonly BoardAttendee[]>();
  readonly live = input(false);
  // Reference clock (ms) captured when the board loaded — drives wait-time display.
  readonly now = input(0);

  readonly addPlayer = output<void>();
  readonly rest = output<BoardAttendee>();
  readonly leave = output<BoardAttendee>();
  readonly unrest = output<BoardAttendee>();
  readonly remove = output<BoardAttendee>();

  protected readonly waiting = computed(() =>
    this.attendees()
      .filter((a) => a.status === 'Waiting')
      .slice()
      .sort((x, y) => x.waitingSince.localeCompare(y.waitingSince)),
  );
  protected readonly resting = computed(() =>
    this.attendees().filter((a) => a.status === 'Resting'),
  );
  // Closed-history roster: everyone who took part, busiest first.
  protected readonly roster = computed(() =>
    this.attendees()
      .filter((a) => a.status !== 'Left' || a.gamesPlayed > 0)
      .slice()
      .sort((x, y) => y.gamesPlayed - x.gamesPlayed),
  );

  protected wait(a: BoardAttendee): number {
    return waitMinutes(a.waitingSince, this.now());
  }
}
