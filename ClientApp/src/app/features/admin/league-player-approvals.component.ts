import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { PlayersApi, Registration, Transfer } from './players.api';
import { StatusColorPipe } from '../../shared/status-color.pipe';

// League-admin view: confirm/reject pending discipline registrations and approve/reject
// pending registration transfers for this league.
@Component({
  selector: 'app-league-player-approvals',
  imports: [StatusColorPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">Player registrations</h2>
    <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
      @for (r of registrations(); track r.id) {
        <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 font-mono text-sm">
          <span class="font-medium text-slate-900 dark:text-slate-100">{{ r.playerName }}</span>
          <span class="text-slate-500 dark:text-slate-400">{{ r.gender }}</span>
          <span class="inline-block rounded bg-slate-200 px-1.5 py-0.5 text-xs dark:bg-slate-700">{{ r.discipline }}</span>
          <span class="text-slate-500 dark:text-slate-400">@ {{ r.clubShortCode }}</span>
          <span [class]="'ml-auto inline-block rounded px-2 py-0.5 text-xs ' + (r.status | statusColor)">{{ r.status }}</span>
          @if (r.status === 'Pending') {
            <button type="button" (click)="confirmReg(r)" class="rounded-md border border-emerald-300 px-2 py-1 text-xs text-emerald-700 hover:bg-emerald-50 dark:border-emerald-800 dark:text-emerald-400 dark:hover:bg-emerald-950">Confirm</button>
            <button type="button" (click)="rejectReg(r)" class="rounded-md border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950">Reject</button>
          }
        </li>
      } @empty {
        <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">No registrations.</li>
      }
    </ul>
    @if (error()) { <p class="mt-2 font-mono text-xs text-red-600 dark:text-red-400" role="alert">{{ error() }}</p> }

    <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900 dark:text-slate-100">Player transfers</h2>
    <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
      @for (t of transfers(); track t.id) {
        <li class="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-3 font-mono text-sm">
          <span class="font-medium text-slate-900 dark:text-slate-100">{{ t.playerName }}</span>
          <span class="inline-block rounded bg-slate-200 px-1.5 py-0.5 text-xs dark:bg-slate-700">{{ t.discipline }}</span>
          <span class="text-slate-600 dark:text-slate-400">{{ t.fromShortCode }} → {{ t.toShortCode }}</span>
          <span class="text-xs text-slate-400 dark:text-slate-500">(release {{ t.releasingApproved ? '✓' : '…' }}, league {{ t.leagueApproved ? '✓' : '…' }})</span>
          <span [class]="'ml-auto inline-block rounded px-2 py-0.5 text-xs ' + (t.status | statusColor)">{{ t.status }}</span>
          @if (t.status === 'Pending') {
            <button type="button" (click)="approveTransfer(t)" class="rounded-md border border-emerald-300 px-2 py-1 text-xs text-emerald-700 hover:bg-emerald-50 dark:border-emerald-800 dark:text-emerald-400 dark:hover:bg-emerald-950">Approve</button>
            <button type="button" (click)="rejectTransfer(t)" class="rounded-md border border-red-300 px-2 py-1 text-xs text-red-700 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950">Reject</button>
          }
        </li>
      } @empty {
        <li class="px-4 py-3 font-mono text-sm text-slate-500 dark:text-slate-400">No transfers.</li>
      }
    </ul>
  `,
})
export class LeaguePlayerApprovalsComponent {
  private readonly api = inject(PlayersApi);

  readonly leagueId = input.required<string>();

  protected readonly registrations = signal<Registration[]>([]);
  protected readonly transfers = signal<Transfer[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.leagueId();
      if (id) this.refresh(id);
    });
  }

  private refresh(id: string): void {
    this.api.listLeagueRegistrations(id).subscribe({ next: (r) => this.registrations.set(r) });
    this.api.listLeagueTransfers(id).subscribe({ next: (t) => this.transfers.set(t) });
  }

  protected confirmReg(r: Registration): void {
    this.error.set(null);
    this.api.confirmRegistration(this.leagueId(), r.id).subscribe({
      next: () => this.refresh(this.leagueId()),
      error: (e: { error?: { title?: string } }) => this.error.set(e?.error?.title ?? 'Could not confirm.'),
    });
  }

  protected rejectReg(r: Registration): void {
    this.api.rejectRegistration(this.leagueId(), r.id).subscribe({ next: () => this.refresh(this.leagueId()) });
  }

  protected approveTransfer(t: Transfer): void {
    this.api.leagueApproveTransfer(this.leagueId(), t.id).subscribe({ next: () => this.refresh(this.leagueId()) });
  }

  protected rejectTransfer(t: Transfer): void {
    this.api.leagueRejectTransfer(this.leagueId(), t.id).subscribe({ next: () => this.refresh(this.leagueId()) });
  }
}
