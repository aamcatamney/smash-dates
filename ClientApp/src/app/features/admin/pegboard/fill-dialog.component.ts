import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { BoardAttendee, FillSuggestion, GameSide, GameType } from '../pegboard.api';
import { ModalComponent } from '../../../shared/modal.component';

// The game types offered in the Fill flow, in display order, with the side size each expects.
// The size is a hint only — an off-size makeup still starts (the server warns, never blocks).
const TYPE_SIZES: ReadonlyArray<{ type: GameType; perSide: number }> = [
  { type: 'Singles', perSide: 1 },
  { type: 'Doubles', perSide: 2 },
  { type: 'Mixed', perSide: 2 },
  { type: 'Funny', perSide: 2 },
];

export interface StartGamePayload {
  readonly type: GameType;
  readonly sideA: string[];
  readonly sideB: string[];
}

// Presentational fill dialog: pick a type, tap waiting players to cycle A → B → unassigned,
// or ask the parent to Suggest a lineup (the result rides back in via [suggestion]). Owns only
// the ephemeral assignment UI; the parent owns the court, the API calls and the suggestion.
@Component({
  selector: 'app-fill-dialog',
  imports: [ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-modal [open]="open()" [title]="title()" (closed)="closed.emit()">
      <div class="grid gap-4">
        <label class="grid gap-1">
          <span
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Game type</span
          >
          <select
            [value]="type()"
            (change)="onTypeChange($event)"
            class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
          >
            @for (t of types; track t.type) {
              <option [value]="t.type">{{ t.type }}</option>
            }
          </select>
        </label>

        <div
          class="flex items-center justify-between font-mono text-xs text-slate-500 dark:text-slate-400"
        >
          <span>Expecting {{ perSide() }} v {{ perSide() }}</span>
          <span>A {{ countA() }} · B {{ countB() }}</span>
        </div>

        <button
          type="button"
          (click)="suggest.emit(type())"
          class="min-h-11 justify-self-start rounded-md border border-slate-300 px-3 py-1.5 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
        >
          Suggest a lineup
        </button>

        <fieldset class="grid gap-2">
          <legend
            class="font-mono text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
          >
            Tap to assign — A then B
          </legend>
          <ul class="max-h-64 space-y-1 overflow-y-auto">
            @for (a of waiting(); track a.id) {
              <li>
                <button
                  type="button"
                  (click)="cycleSide(a.id)"
                  [attr.aria-pressed]="sideOf(a.id) !== null"
                  class="flex min-h-11 w-full items-center justify-between rounded-md border px-3 py-2 text-left font-mono text-sm"
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
                    <span
                      class="rounded bg-slate-900 px-2 py-0.5 text-xs text-amber-300 dark:bg-amber-400 dark:text-slate-900"
                      >{{ s }}</span
                    >
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
          class="min-h-11 justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          Start game
        </button>
      </div>
    </app-modal>
  `,
})
export class FillDialogComponent {
  readonly open = input(false);
  readonly courtLabel = input('');
  readonly waiting = input.required<readonly BoardAttendee[]>();
  // A suggestion pushed from the parent (the result of its Suggest API call) seeds the assignment.
  readonly suggestion = input<FillSuggestion | null>(null);

  readonly suggest = output<GameType>();
  readonly start = output<StartGamePayload>();
  readonly closed = output<void>();

  protected readonly types = TYPE_SIZES;
  protected readonly type = signal<GameType>('Doubles');
  // attendanceId -> assigned side. A plain map signal keeps template lookups arrow-free.
  protected readonly assignments = signal<ReadonlyMap<string, GameSide>>(new Map());

  protected readonly title = computed(() =>
    this.courtLabel() ? `Fill ${this.courtLabel()}` : 'Fill court',
  );
  protected readonly perSide = computed(
    () => this.types.find((t) => t.type === this.type())?.perSide ?? 2,
  );
  protected readonly countA = computed(() => this.tally('A'));
  protected readonly countB = computed(() => this.tally('B'));
  protected readonly canStart = computed(() => this.countA() > 0 && this.countB() > 0);

  constructor() {
    // Re-opening the dialog clears any prior assignment.
    effect(() => {
      if (this.open()) {
        this.type.set('Doubles');
        this.assignments.set(new Map());
      }
    });
    // A fresh suggestion seeds the sides.
    effect(() => {
      const s = this.suggestion();
      if (!s) return;
      const map = new Map<string, GameSide>();
      for (const id of s.sideA) map.set(id, 'A');
      for (const id of s.sideB) map.set(id, 'B');
      this.assignments.set(map);
    });
  }

  protected onTypeChange(event: Event): void {
    this.type.set((event.target as HTMLSelectElement).value as GameType);
  }

  protected sideOf(attendanceId: string): GameSide | null {
    return this.assignments().get(attendanceId) ?? null;
  }

  // Tap an attendee to cycle unassigned -> A -> B -> unassigned.
  protected cycleSide(attendanceId: string): void {
    const next = new Map(this.assignments());
    const current = next.get(attendanceId);
    if (current === undefined) next.set(attendanceId, 'A');
    else if (current === 'A') next.set(attendanceId, 'B');
    else next.delete(attendanceId);
    this.assignments.set(next);
  }

  protected onStart(): void {
    if (!this.canStart()) return;
    const sideA: string[] = [];
    const sideB: string[] = [];
    for (const [id, side] of this.assignments()) {
      if (side === 'A') sideA.push(id);
      else sideB.push(id);
    }
    this.start.emit({ type: this.type(), sideA, sideB });
  }

  private tally(side: GameSide): number {
    let n = 0;
    for (const s of this.assignments().values()) if (s === side) n++;
    return n;
  }
}
