import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { BoardAttendee } from '../pegboard.api';
import { ModalComponent } from '../../../shared/modal.component';

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
  imports: [ModalComponent],
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
              <span class="min-w-0 truncate font-mono text-sm font-medium text-slate-900 dark:text-slate-100">{{
                a.displayName
              }}</span>
              <button
                type="button"
                [attr.aria-label]="'Actions for ' + a.displayName"
                aria-haspopup="dialog"
                (click)="menuFor.set(a)"
                class="-mr-1 inline-flex min-h-11 min-w-11 shrink-0 items-center justify-center rounded font-mono text-lg text-slate-500 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:text-slate-400 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
              >
                ⋯
              </button>
            </div>
            <p class="mt-1 font-mono text-xs text-slate-500 dark:text-slate-400">
              {{ a.gender }}@if (a.grade !== null) { · G{{ a.grade }} } · waiting {{ wait(a) }}m ·
              {{ a.gamesPlayed }} played · {{ a.gamesWon }} won
            </p>
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

      <!-- Per-attendee action sheet: keeps each queue row to one line and separates the
           destructive Remove from the routine Rest / Leave. -->
      <app-modal
        [open]="menuFor() !== null"
        [title]="menuFor()?.displayName ?? ''"
        (closed)="menuFor.set(null)"
      >
        <div class="grid gap-2">
          <button
            type="button"
            (click)="pick('rest')"
            class="min-h-11 rounded-md border border-slate-300 px-4 py-2.5 text-left font-mono text-sm text-slate-700 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
          >
            Rest
          </button>
          <button
            type="button"
            (click)="pick('leave')"
            class="min-h-11 rounded-md border border-slate-300 px-4 py-2.5 text-left font-mono text-sm text-slate-700 hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
          >
            Leave for the night
          </button>
          <button
            type="button"
            (click)="pick('remove')"
            class="min-h-11 rounded-md border border-red-300 px-4 py-2.5 text-left font-mono text-sm text-red-700 hover:bg-red-50 focus:outline-none focus:ring-2 focus:ring-red-600 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
          >
            Remove from board
          </button>
        </div>
      </app-modal>
    } @else {
      <h2
        class="font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100"
      >
        Attendees <span class="text-slate-400">({{ roster().length }})</span>
      </h2>
      <ol class="mt-3 space-y-2">
        @for (a of roster(); track a.id) {
          <li>
            <button
              type="button"
              [attr.aria-label]="'Match history and time for ' + a.displayName"
              (click)="selectPlayer.emit(a)"
              class="flex w-full items-center justify-between gap-2 rounded-md border border-slate-200 bg-white px-3 py-2 text-left hover:bg-slate-50 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-800 dark:bg-slate-900 dark:hover:bg-slate-800 dark:focus:ring-slate-100"
            >
              <span class="font-mono text-sm text-slate-700 dark:text-slate-300">{{
                a.displayName
              }}</span>
              <span class="font-mono text-xs text-slate-500 dark:text-slate-400">
                {{ a.gamesPlayed }} played · {{ a.gamesWon }} won ›
              </span>
            </button>
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
  // Closed-history: a roster row was clicked to see that player's matches and time split.
  readonly selectPlayer = output<BoardAttendee>();

  // The attendee whose action sheet is open (null = closed).
  protected readonly menuFor = signal<BoardAttendee | null>(null);

  // Fire the chosen action for the open attendee and close the sheet. Remove still routes
  // through the parent's confirm step.
  protected pick(action: 'rest' | 'leave' | 'remove'): void {
    const a = this.menuFor();
    if (!a) return;
    this.menuFor.set(null);
    if (action === 'rest') this.rest.emit(a);
    else if (action === 'leave') this.leave.emit(a);
    else this.remove.emit(a);
  }

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
