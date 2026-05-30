import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Discipline, Player, PlayerClubType, PlayersApi, PlayerLink, Registration, Transfer } from './players.api';
import { ModalComponent } from '../../shared/modal.component';
import { StatusColorPipe } from '../../shared/status-color.pipe';

interface LeagueOption {
  id: string;
  name: string;
}

// Club-side player management: affiliations (Member/Visitor), discipline registrations,
// and initiating / approving transfers. The parent passes the club id and the leagues the
// club is an accepted member of (for the register / transfer-in selects).
@Component({
  selector: 'app-club-players',
  imports: [ReactiveFormsModule, ModalComponent, StatusColorPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mt-10 flex items-center justify-between">
      <h2 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">Players</h2>
      <div class="flex gap-2">
        <button type="button" (click)="openTransferIn()" class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800">Transfer in</button>
        <button type="button" (click)="openAdd()" class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800">＋ Add player</button>
      </div>
    </div>
    <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
      @for (p of players(); track p.playerId) {
        <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 font-mono text-sm">
          <span class="font-medium text-slate-900 dark:text-slate-100">{{ p.fullName }}</span>
          <span class="text-slate-500 dark:text-slate-400">{{ p.gender }}</span>
          <span class="inline-block rounded px-2 py-0.5 text-xs " [class]="p.type === 'Member' ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300' : 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200'">{{ p.type }}</span>
          @for (r of regsFor(p.playerId); track r.id) {
            <span [class]="'inline-block rounded px-1.5 py-0.5 text-xs ' + (r.status | statusColor)">{{ r.discipline }} · {{ r.leagueName }} · {{ r.status }}</span>
          }
          <span class="ml-auto flex gap-2">
            @if (p.type === 'Member') {
              <button type="button" (click)="openRegister(p)" class="rounded-md border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800">Register</button>
            }
            <button type="button" (click)="toggleType(p)" class="rounded-md border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800">Make {{ p.type === 'Member' ? 'visitor' : 'member' }}</button>
            <button type="button" (click)="remove(p)" class="rounded-md border border-red-300 px-2 py-0.5 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950">Remove</button>
          </span>
        </li>
      } @empty {
        <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">No players.</li>
      }
    </ul>
    @if (error()) { <p class="mt-2 font-mono text-xs text-red-600 dark:text-red-400" role="alert">{{ error() }}</p> }

    <!-- Add / link player -->
    <app-modal [open]="addOpen()" title="Add player" (closed)="addOpen.set(false)">
      <div class="grid gap-4 font-mono text-sm">
        <div class="grid gap-2">
          <span class="text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Link an existing player</span>
          <input type="text" [formControl]="searchControl" (input)="onSearch()" placeholder="Search by name…"
            class="rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100" />
          @for (r of searchResults(); track r.id) {
            <div class="flex items-center justify-between rounded border border-slate-200 px-3 py-1.5 dark:border-slate-800">
              <span>{{ r.fullName }} <span class="text-slate-400">{{ r.gender }}</span></span>
              <span class="flex gap-1">
                <button type="button" (click)="linkExisting(r, 'Member')" class="rounded border border-slate-300 px-2 py-0.5 text-xs dark:border-slate-700">+ Member</button>
                <button type="button" (click)="linkExisting(r, 'Visitor')" class="rounded border border-slate-300 px-2 py-0.5 text-xs dark:border-slate-700">+ Visitor</button>
              </span>
            </div>
          }
        </div>
        <form [formGroup]="createForm" (ngSubmit)="createNew()" class="grid gap-2 border-t border-slate-200 pt-3 dark:border-slate-800">
          <span class="text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">Or create a new player</span>
          <input type="text" formControlName="fullName" placeholder="Full name"
            class="rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100" />
          <div class="flex gap-2">
            <select formControlName="gender" class="rounded-md border border-slate-300 px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
              <option value="Male">Male</option>
              <option value="Female">Female</option>
            </select>
            <select formControlName="type" class="rounded-md border border-slate-300 px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
              <option value="Member">Member</option>
              <option value="Visitor">Visitor</option>
            </select>
            <button type="submit" [disabled]="createForm.invalid" class="rounded-md bg-slate-900 px-3 py-1 text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900">Create</button>
          </div>
        </form>
      </div>
    </app-modal>

    <!-- Register discipline -->
    <app-modal [open]="registerFor() !== null" title="Register player" (closed)="registerFor.set(null)">
      <form [formGroup]="registerForm" (ngSubmit)="submitRegister()" class="grid gap-3 font-mono text-sm">
        <p class="text-slate-600 dark:text-slate-400">{{ registerFor()?.fullName }}</p>
        <select formControlName="leagueId" class="rounded-md border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
          <option value="">-- league --</option>
          @for (l of leagues(); track l.id) { <option [value]="l.id">{{ l.name }}</option> }
        </select>
        <select formControlName="discipline" class="rounded-md border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
          <option value="Level">Level</option>
          <option value="Mixed">Mixed</option>
        </select>
        <button type="submit" [disabled]="registerForm.invalid" class="justify-self-start rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900">Request registration</button>
        @if (modalError()) { <p class="text-red-600 dark:text-red-400" role="alert">{{ modalError() }}</p> }
      </form>
    </app-modal>

    <!-- Transfer in -->
    <app-modal [open]="transferOpen()" title="Transfer a player in" (closed)="transferOpen.set(false)">
      <div class="grid gap-3 font-mono text-sm">
        <input type="text" [formControl]="transferSearchControl" (input)="onTransferSearch()" placeholder="Search player by name…"
          class="rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100" />
        @for (r of transferResults(); track r.id) {
          <button type="button" (click)="pickTransferPlayer(r)" [class]="'rounded border px-3 py-1.5 text-left ' + (transferPlayer()?.id === r.id ? 'border-slate-900 dark:border-slate-100' : 'border-slate-200 dark:border-slate-800')">
            {{ r.fullName }} <span class="text-slate-400">{{ r.gender }}</span>
          </button>
        }
        <form [formGroup]="transferForm" (ngSubmit)="submitTransfer()" class="grid gap-2 border-t border-slate-200 pt-3 dark:border-slate-800">
          <select formControlName="leagueId" class="rounded-md border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
            <option value="">-- league --</option>
            @for (l of leagues(); track l.id) { <option [value]="l.id">{{ l.name }}</option> }
          </select>
          <select formControlName="discipline" class="rounded-md border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
            <option value="Level">Level</option>
            <option value="Mixed">Mixed</option>
          </select>
          <button type="submit" [disabled]="transferForm.invalid || transferPlayer() === null" class="justify-self-start rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900">Request transfer</button>
          @if (modalError()) { <p class="text-red-600 dark:text-red-400" role="alert">{{ modalError() }}</p> }
        </form>
      </div>
    </app-modal>

    <!-- Transfers list -->
    <h3 class="mt-8 font-mono text-sm font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300">Transfers</h3>
    <ul class="mt-2 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
      @for (t of transfers(); track t.id) {
        <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 font-mono text-sm">
          <span class="font-medium text-slate-900 dark:text-slate-100">{{ t.playerName }}</span>
          <span class="inline-block rounded bg-slate-200 px-1.5 py-0.5 text-xs dark:bg-slate-700">{{ t.discipline }}</span>
          <span class="text-slate-600 dark:text-slate-400">{{ t.fromShortCode }} → {{ t.toShortCode }}</span>
          <span [class]="'ml-auto inline-block rounded px-2 py-0.5 text-xs ' + (t.status | statusColor)">{{ t.status }}</span>
          @if (t.status === 'Pending' && t.fromClubId === clubId()) {
            <button type="button" (click)="approveRelease(t)" class="rounded-md border border-emerald-300 px-2 py-1 text-xs text-emerald-700 hover:bg-emerald-50 dark:border-emerald-800 dark:text-emerald-400 dark:hover:bg-emerald-950">Release</button>
            <button type="button" (click)="rejectRelease(t)" class="rounded-md border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950">Reject</button>
          }
        </li>
      } @empty {
        <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">No transfers.</li>
      }
    </ul>
  `,
})
export class ClubPlayersComponent {
  private readonly api = inject(PlayersApi);

  readonly clubId = input.required<string>();
  readonly leagues = input<LeagueOption[]>([]);

  protected readonly players = signal<PlayerLink[]>([]);
  protected readonly registrations = signal<Registration[]>([]);
  protected readonly transfers = signal<Transfer[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly modalError = signal<string | null>(null);

  protected readonly addOpen = signal(false);
  protected readonly registerFor = signal<PlayerLink | null>(null);
  protected readonly transferOpen = signal(false);
  protected readonly searchResults = signal<Player[]>([]);
  protected readonly transferResults = signal<Player[]>([]);
  protected readonly transferPlayer = signal<Player | null>(null);

  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly transferSearchControl = new FormControl('', { nonNullable: true });

  protected readonly createForm = new FormGroup({
    fullName: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    gender: new FormControl<'Male' | 'Female'>('Male', { nonNullable: true }),
    type: new FormControl<PlayerClubType>('Member', { nonNullable: true }),
  });
  protected readonly registerForm = new FormGroup({
    leagueId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    discipline: new FormControl<Discipline>('Level', { nonNullable: true }),
  });
  protected readonly transferForm = new FormGroup({
    leagueId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    discipline: new FormControl<Discipline>('Level', { nonNullable: true }),
  });

  protected regsFor(playerId: string): Registration[] {
    return this.registrations().filter((r) => r.playerId === playerId && r.status !== 'Rejected');
  }

  constructor() {
    effect(() => {
      if (this.clubId()) this.refresh();
    });
  }

  private refresh(): void {
    const id = this.clubId();
    this.api.listClubPlayers(id).subscribe({ next: (p) => this.players.set(p) });
    this.api.listClubRegistrations(id).subscribe({ next: (r) => this.registrations.set(r) });
    this.api.listClubTransfers(id).subscribe({ next: (t) => this.transfers.set(t) });
  }

  protected openAdd(): void {
    this.searchControl.reset('');
    this.searchResults.set([]);
    this.createForm.reset({ fullName: '', gender: 'Male', type: 'Member' });
    this.addOpen.set(true);
  }

  protected onSearch(): void {
    const q = this.searchControl.value.trim();
    if (q.length < 2) {
      this.searchResults.set([]);
      return;
    }
    this.api.searchPlayers(q).subscribe({ next: (rows) => this.searchResults.set(rows) });
  }

  protected linkExisting(player: Player, type: PlayerClubType): void {
    this.api.linkExistingPlayer(this.clubId(), player.id, type).subscribe({
      next: () => {
        this.addOpen.set(false);
        this.refresh();
      },
    });
  }

  protected createNew(): void {
    const { fullName, gender, type } = this.createForm.getRawValue();
    this.api.addNewPlayer(this.clubId(), fullName.trim(), gender, type).subscribe({
      next: () => {
        this.addOpen.set(false);
        this.refresh();
      },
    });
  }

  protected toggleType(p: PlayerLink): void {
    const next: PlayerClubType = p.type === 'Member' ? 'Visitor' : 'Member';
    this.error.set(null);
    this.api.updateLinkType(this.clubId(), p.playerId, next).subscribe({ next: () => this.refresh() });
  }

  protected remove(p: PlayerLink): void {
    this.error.set(null);
    this.api.unlinkPlayer(this.clubId(), p.playerId).subscribe({
      next: () => this.refresh(),
      error: (e: { error?: { title?: string } }) => this.error.set(e?.error?.title ?? 'Could not remove player.'),
    });
  }

  protected openRegister(p: PlayerLink): void {
    this.modalError.set(null);
    this.registerForm.reset({ leagueId: '', discipline: 'Level' });
    this.registerFor.set(p);
  }

  protected submitRegister(): void {
    const player = this.registerFor();
    if (player === null) return;
    const { leagueId, discipline } = this.registerForm.getRawValue();
    this.modalError.set(null);
    this.api.requestRegistration(this.clubId(), player.playerId, leagueId, discipline).subscribe({
      next: () => {
        this.registerFor.set(null);
        this.refresh();
      },
      error: (e: { error?: { title?: string } }) => this.modalError.set(e?.error?.title ?? 'Could not register.'),
    });
  }

  protected openTransferIn(): void {
    this.modalError.set(null);
    this.transferSearchControl.reset('');
    this.transferResults.set([]);
    this.transferPlayer.set(null);
    this.transferForm.reset({ leagueId: '', discipline: 'Level' });
    this.transferOpen.set(true);
  }

  protected onTransferSearch(): void {
    const q = this.transferSearchControl.value.trim();
    if (q.length < 2) {
      this.transferResults.set([]);
      return;
    }
    this.api.searchPlayers(q).subscribe({ next: (rows) => this.transferResults.set(rows) });
  }

  protected pickTransferPlayer(p: Player): void {
    this.transferPlayer.set(p);
  }

  protected submitTransfer(): void {
    const player = this.transferPlayer();
    if (player === null) return;
    const { leagueId, discipline } = this.transferForm.getRawValue();
    this.modalError.set(null);
    this.api.openTransfer(this.clubId(), player.id, leagueId, discipline).subscribe({
      next: () => {
        this.transferOpen.set(false);
        this.refresh();
      },
      error: (e: { error?: { title?: string } }) => this.modalError.set(e?.error?.title ?? 'Could not open transfer.'),
    });
  }

  protected approveRelease(t: Transfer): void {
    this.api.clubApproveTransfer(this.clubId(), t.id).subscribe({ next: () => this.refresh() });
  }

  protected rejectRelease(t: Transfer): void {
    this.api.clubRejectTransfer(this.clubId(), t.id).subscribe({ next: () => this.refresh() });
  }
}
