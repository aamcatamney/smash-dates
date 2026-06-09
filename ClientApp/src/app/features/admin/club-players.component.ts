import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { AuthStore } from '../../core/auth/auth.store';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  Discipline,
  PlayerClubType,
  PlayersApi,
  PlayerLink,
  Registration,
  Transfer,
  TransferCandidate,
} from './players.api';
import { ModalComponent } from '../../shared/modal.component';
import { StatusColorPipe } from '../../shared/status-color.pipe';
import { CsvImportComponent } from '../../shared/csv-import.component';
import { ImportResult } from '../../shared/import-result';

interface LeagueOption {
  id: string;
  name: string;
}

// Club-side player management: affiliations (Member/Visitor), discipline registrations,
// and initiating / approving transfers. Players are always created fresh here — there is no
// link-an-existing-player-by-name step. The parent passes the club id and the leagues the
// club is an accepted member of (for the register select).
@Component({
  selector: 'app-club-players',
  imports: [ReactiveFormsModule, ModalComponent, StatusColorPipe, CsvImportComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mt-10 flex items-center justify-between">
      <h2 class="font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">Players</h2>
      @if (canManage()) {
        <div class="flex gap-2">
          <button
            type="button"
            (click)="openImport()"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            Import CSV
          </button>
          <button
            type="button"
            (click)="openTransferIn()"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            Transfer in
          </button>
          <button
            type="button"
            (click)="openAdd()"
            class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
          >
            ＋ Add player
          </button>
        </div>
      }
    </div>

    <app-csv-import
      [open]="importOpen()"
      title="Import players"
      [columns]="importColumns"
      sample="Alice Tan,Female,2,false"
      [result]="importResult()"
      [busy]="importBusy()"
      (submit)="onImport($event)"
      (closed)="importOpen.set(false)"
    />
    <ul
      class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900"
    >
      @for (p of players(); track p.playerId) {
        <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 font-mono text-sm">
          <span class="font-medium text-slate-900 dark:text-slate-100">{{ p.fullName }}</span>
          <span class="text-slate-500 dark:text-slate-400">{{ p.gender }}</span>
          <span
            class="inline-block rounded px-2 py-0.5 text-xs "
            [class]="
              p.type === 'Member'
                ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300'
                : 'bg-slate-200 text-slate-700 dark:bg-slate-700 dark:text-slate-200'
            "
            >{{ p.type }}</span
          >
          @for (r of regsFor(p.playerId); track r.id) {
            <span [class]="'inline-block rounded px-1.5 py-0.5 text-xs ' + (r.status | statusColor)"
              >{{ r.discipline }} · {{ r.leagueName }} · {{ r.status }}</span
            >
          }
          @if (canManage()) {
            <span class="ml-auto flex gap-2">
              @if (p.type === 'Member') {
                <button
                  type="button"
                  (click)="openRegister(p)"
                  class="rounded-md border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
                >
                  Register
                </button>
              }
              <button
                type="button"
                (click)="toggleType(p)"
                class="rounded-md border border-slate-300 px-2 py-0.5 text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
              >
                Make {{ p.type === 'Member' ? 'visitor' : 'member' }}
              </button>
              <button
                type="button"
                (click)="remove(p)"
                class="rounded-md border border-red-300 px-2 py-0.5 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
              >
                Remove
              </button>
            </span>
          }
        </li>
      } @empty {
        <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">No players.</li>
      }
    </ul>
    @if (error()) {
      <p class="mt-2 font-mono text-xs text-red-600 dark:text-red-400" role="alert">
        {{ error() }}
      </p>
    }

    <!-- Add player -->
    <app-modal [open]="addOpen()" title="Add player" (closed)="addOpen.set(false)">
      <div class="grid gap-4 font-mono text-sm">
        <form [formGroup]="createForm" (ngSubmit)="createNew()" class="grid gap-2">
          <span class="text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400"
            >Create a new player</span
          >
          <input
            type="text"
            formControlName="fullName"
            placeholder="Full name"
            aria-label="New player full name"
            class="rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
          />
          <div class="flex gap-2">
            <select
              formControlName="gender"
              aria-label="New player gender"
              class="rounded-md border border-slate-300 px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
            >
              <option value="Male">Male</option>
              <option value="Female">Female</option>
            </select>
            <select
              formControlName="type"
              aria-label="New player affiliation type"
              class="rounded-md border border-slate-300 px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
            >
              <option value="Member">Member</option>
              <option value="Visitor">Visitor</option>
            </select>
            <button
              type="submit"
              [disabled]="createForm.invalid"
              class="rounded-md bg-slate-900 px-3 py-1 text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
            >
              Create
            </button>
          </div>
        </form>
      </div>
    </app-modal>

    <!-- Register discipline -->
    <app-modal
      [open]="registerFor() !== null"
      title="Register player"
      (closed)="registerFor.set(null)"
    >
      <form
        [formGroup]="registerForm"
        (ngSubmit)="submitRegister()"
        class="grid gap-3 font-mono text-sm"
      >
        <p class="text-slate-600 dark:text-slate-400">{{ registerFor()?.fullName }}</p>
        <select
          formControlName="leagueId"
          aria-label="League"
          class="rounded-md border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
        >
          <option value="">-- league --</option>
          @for (l of leagues(); track l.id) {
            <option [value]="l.id">{{ l.name }}</option>
          }
        </select>
        <select
          formControlName="discipline"
          aria-label="Discipline"
          class="rounded-md border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
        >
          <option value="Level">Level</option>
          <option value="Mixed">Mixed</option>
        </select>
        <button
          type="submit"
          [disabled]="registerForm.invalid"
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-amber-300 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900"
        >
          Request registration
        </button>
        @if (modalError()) {
          <p class="text-red-600 dark:text-red-400" role="alert">{{ modalError() }}</p>
        }
      </form>
    </app-modal>

    <!-- Transfer in -->
    <app-modal
      [open]="transferOpen()"
      title="Transfer a player in"
      (closed)="transferOpen.set(false)"
    >
      <div class="grid gap-3 font-mono text-sm">
        <input
          type="text"
          [formControl]="transferSearchControl"
          (input)="onTransferSearch()"
          placeholder="Search player by name…"
          aria-label="Search players by name"
          class="rounded-md border border-slate-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:focus:ring-slate-100"
        />
        <p class="text-xs text-slate-500 dark:text-slate-400">
          Only players with a confirmed registration in a league this club belongs to.
        </p>
        @for (r of transferResults(); track r.playerId + r.leagueId + r.discipline) {
          <button
            type="button"
            (click)="pickTransfer(r)"
            [class]="
              'grid gap-0.5 rounded border px-3 py-1.5 text-left ' +
              (isPicked(r)
                ? 'border-slate-900 dark:border-slate-100'
                : 'border-slate-200 dark:border-slate-800')
            "
          >
            <span
              >{{ r.fullName }} <span class="text-slate-400">{{ r.gender }}</span></span
            >
            <span class="text-xs text-slate-500 dark:text-slate-400"
              >{{ r.leagueName }} · {{ r.discipline }} · from {{ r.currentClubShortCode }}</span
            >
          </button>
        } @empty {
          @if (transferSearchControl.value.trim().length >= 2) {
            <p class="text-slate-500 dark:text-slate-400">No transferable players found.</p>
          }
        }
        @if (transferPick(); as p) {
          <div class="grid gap-2 border-t border-slate-200 pt-3 dark:border-slate-800">
            <p class="text-slate-700 dark:text-slate-300">
              Transferring <span class="font-semibold">{{ p.fullName }}</span> —
              {{ p.leagueName }} / {{ p.discipline }} from {{ p.currentClubShortCode }}
            </p>
            <button
              type="button"
              (click)="submitTransfer()"
              class="justify-self-start rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-amber-300 dark:bg-amber-400 dark:text-slate-900"
            >
              Request transfer
            </button>
          </div>
        }
        @if (modalError()) {
          <p class="text-red-600 dark:text-red-400" role="alert">{{ modalError() }}</p>
        }
      </div>
    </app-modal>

    <!-- Transfers list -->
    <h3
      class="mt-8 font-mono text-sm font-semibold uppercase tracking-wider text-slate-700 dark:text-slate-300"
    >
      Transfers
    </h3>
    <ul
      class="mt-2 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900"
    >
      @for (t of transfers(); track t.id) {
        <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 font-mono text-sm">
          <span class="font-medium text-slate-900 dark:text-slate-100">{{ t.playerName }}</span>
          <span class="inline-block rounded bg-slate-200 px-1.5 py-0.5 text-xs dark:bg-slate-700">{{
            t.discipline
          }}</span>
          <span class="text-slate-600 dark:text-slate-400"
            >{{ t.fromShortCode }} → {{ t.toShortCode }}</span
          >
          <span
            [class]="'ml-auto inline-block rounded px-2 py-0.5 text-xs ' + (t.status | statusColor)"
            >{{ t.status }}</span
          >
          @if (t.status === 'Pending' && t.fromClubId === clubId()) {
            <button
              type="button"
              (click)="approveRelease(t)"
              class="rounded-md border border-emerald-300 px-2 py-1 text-xs text-emerald-700 hover:bg-emerald-50 dark:border-emerald-800 dark:text-emerald-400 dark:hover:bg-emerald-950"
            >
              Release
            </button>
            <button
              type="button"
              (click)="rejectRelease(t)"
              class="rounded-md border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950"
            >
              Reject
            </button>
          }
        </li>
      } @empty {
        <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">
          No transfers.
        </li>
      }
    </ul>
  `,
})
export class ClubPlayersComponent {
  private readonly api = inject(PlayersApi);
  private readonly store = inject(AuthStore);

  readonly clubId = input.required<string>();
  protected readonly canManage = computed(() => this.store.isClubAdmin(this.clubId()));
  readonly leagues = input<LeagueOption[]>([]);
  readonly playerCount = output<number>();

  protected readonly players = signal<PlayerLink[]>([]);
  protected readonly registrations = signal<Registration[]>([]);
  protected readonly transfers = signal<Transfer[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly modalError = signal<string | null>(null);

  protected readonly addOpen = signal(false);
  protected readonly importOpen = signal(false);
  protected readonly importBusy = signal(false);
  protected readonly importResult = signal<ImportResult | null>(null);
  protected readonly importColumns = ['name', 'gender', 'grade'];
  protected readonly registerFor = signal<PlayerLink | null>(null);
  protected readonly transferOpen = signal(false);
  protected readonly transferResults = signal<TransferCandidate[]>([]);
  protected readonly transferPick = signal<TransferCandidate | null>(null);

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
    this.api.listClubPlayers(id).subscribe({
      next: (p) => {
        this.players.set(p);
        this.playerCount.emit(p.length);
      },
    });
    this.api.listClubRegistrations(id).subscribe({ next: (r) => this.registrations.set(r) });
    this.api.listClubTransfers(id).subscribe({ next: (t) => this.transfers.set(t) });
  }

  protected openImport(): void {
    this.importResult.set(null);
    this.importOpen.set(true);
  }

  protected onImport(csv: string): void {
    this.importBusy.set(true);
    this.api.importClubPlayers(this.clubId(), csv).subscribe({
      next: (result) => {
        this.importBusy.set(false);
        this.importResult.set(result);
        this.refresh();
      },
      error: () => this.importBusy.set(false),
    });
  }

  protected openAdd(): void {
    this.createForm.reset({ fullName: '', gender: 'Male', type: 'Member' });
    this.addOpen.set(true);
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
    this.api
      .updateLinkType(this.clubId(), p.playerId, next)
      .subscribe({ next: () => this.refresh() });
  }

  protected remove(p: PlayerLink): void {
    this.error.set(null);
    this.api.unlinkPlayer(this.clubId(), p.playerId).subscribe({
      next: () => this.refresh(),
      error: (e: { error?: { title?: string } }) =>
        this.error.set(e?.error?.title ?? 'Could not remove player.'),
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
      error: (e: { error?: { title?: string } }) =>
        this.modalError.set(e?.error?.title ?? 'Could not register.'),
    });
  }

  protected openTransferIn(): void {
    this.modalError.set(null);
    this.transferSearchControl.reset('');
    this.transferResults.set([]);
    this.transferPick.set(null);
    this.transferOpen.set(true);
  }

  protected onTransferSearch(): void {
    const q = this.transferSearchControl.value.trim();
    this.transferPick.set(null);
    if (q.length < 2) {
      this.transferResults.set([]);
      return;
    }
    this.api
      .transferCandidates(this.clubId(), q)
      .subscribe({ next: (rows) => this.transferResults.set(rows) });
  }

  protected pickTransfer(c: TransferCandidate): void {
    this.transferPick.set(c);
  }

  protected isPicked(c: TransferCandidate): boolean {
    const p = this.transferPick();
    return (
      p !== null &&
      p.playerId === c.playerId &&
      p.leagueId === c.leagueId &&
      p.discipline === c.discipline
    );
  }

  protected submitTransfer(): void {
    const pick = this.transferPick();
    if (pick === null) return;
    this.modalError.set(null);
    this.api.openTransfer(this.clubId(), pick.playerId, pick.leagueId, pick.discipline).subscribe({
      next: () => {
        this.transferOpen.set(false);
        this.refresh();
      },
      error: (e: { error?: { title?: string } }) =>
        this.modalError.set(e?.error?.title ?? 'Could not open transfer.'),
    });
  }

  protected approveRelease(t: Transfer): void {
    this.api.clubApproveTransfer(this.clubId(), t.id).subscribe({ next: () => this.refresh() });
  }

  protected rejectRelease(t: Transfer): void {
    this.api.clubRejectTransfer(this.clubId(), t.id).subscribe({ next: () => this.refresh() });
  }
}
