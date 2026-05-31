import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { PlayersApi, PlayerLink, SquadMember } from './players.api';

// One team's squad: lists members and lets a club admin add an (eligible) member or remove
// one. Eligibility is enforced server-side; an ineligible add surfaces the 409 reason here.
@Component({
  selector: 'app-team-squad',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mt-2 rounded-md border border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-950">
      <ul class="grid gap-1">
        @for (m of squad(); track m.playerId) {
          <li class="flex items-center justify-between text-xs">
            <span>{{ m.fullName }} <span class="text-slate-400 dark:text-slate-500">{{ m.gender }}</span></span>
            <button type="button" (click)="remove(m)" class="rounded border border-red-300 px-2 py-0.5 text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950">Remove</button>
          </li>
        } @empty {
          <li class="text-xs text-slate-500 dark:text-slate-400">No players in this squad.</li>
        }
      </ul>
      <form (ngSubmit)="add()" class="mt-2 flex items-center gap-2">
        <select [formControl]="pick" aria-label="Add squad member" class="rounded-md border border-slate-300 px-2 py-1 text-xs dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
          <option value="">-- member --</option>
          @for (c of candidates(); track c.playerId) {
            <option [value]="c.playerId">{{ c.fullName }} ({{ c.gender }})</option>
          }
        </select>
        <button type="submit" [disabled]="!pick.value" class="rounded-md bg-slate-900 px-3 py-1 text-xs font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900">Add</button>
      </form>
      @if (error()) { <p class="mt-1 text-xs text-red-600 dark:text-red-400" role="alert">{{ error() }}</p> }
    </div>
  `,
})
export class TeamSquadComponent {
  private readonly api = inject(PlayersApi);

  readonly clubId = input.required<string>();
  readonly teamId = input.required<string>();

  protected readonly squad = signal<SquadMember[]>([]);
  protected readonly members = signal<PlayerLink[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly pick = new FormControl('', { nonNullable: true });

  // Club Members not already in the squad are the candidates to add.
  protected readonly candidates = computed(() => {
    const inSquad = new Set(this.squad().map((m) => m.playerId));
    return this.members().filter((p) => p.type === 'Member' && !inSquad.has(p.playerId));
  });

  constructor() {
    effect(() => {
      if (this.clubId() && this.teamId()) this.load();
    });
  }

  private load(): void {
    this.api.listSquad(this.clubId(), this.teamId()).subscribe({ next: (s) => this.squad.set(s) });
    this.api.listClubPlayers(this.clubId()).subscribe({ next: (p) => this.members.set(p) });
  }

  protected add(): void {
    const playerId = this.pick.value;
    if (!playerId) return;
    this.error.set(null);
    this.api.addToSquad(this.clubId(), this.teamId(), playerId).subscribe({
      next: () => {
        this.pick.reset('');
        this.load();
      },
      error: (e: { error?: { title?: string } }) => this.error.set(e?.error?.title ?? 'Could not add player.'),
    });
  }

  protected remove(m: SquadMember): void {
    this.api.removeFromSquad(this.clubId(), this.teamId(), m.playerId).subscribe({ next: () => this.load() });
  }
}
