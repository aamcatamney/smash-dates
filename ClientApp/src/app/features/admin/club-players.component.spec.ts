import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { ClubPlayersComponent } from './club-players.component';
import { PlayersApi, TransferCandidate } from './players.api';

function apiMock(overrides: Partial<PlayersApi> = {}): PlayersApi {
  return {
    listClubPlayers: vi.fn(() => of([])),
    listClubRegistrations: vi.fn(() => of([])),
    listClubTransfers: vi.fn(() => of([])),
    updateLinkType: vi.fn(() => of(void 0)),
    unlinkPlayer: vi.fn(() => of(void 0)),
    transferCandidates: vi.fn(() => of([])),
    ...overrides,
  } as unknown as PlayersApi;
}

function create(api: PlayersApi) {
  TestBed.configureTestingModule({ providers: [{ provide: PlayersApi, useValue: api }] });
  const fixture = TestBed.createComponent(ClubPlayersComponent);
  fixture.componentRef.setInput('clubId', 'club-1');
  fixture.componentRef.setInput('leagues', []);
  return fixture.componentInstance as unknown as Record<string, any>;
}

const candidate = (over: Partial<TransferCandidate> = {}): TransferCandidate => ({
  playerId: 'p1',
  fullName: 'Jane Smith',
  gender: 'Female',
  leagueId: 'l1',
  leagueName: 'Mens 1',
  discipline: 'Level',
  currentClubShortCode: 'ACME',
  ...over,
});

describe('ClubPlayersComponent', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('regsFor returns a player\'s non-rejected registrations only', () => {
    const c = create(apiMock());
    c['registrations'].set([
      { id: 'a', playerId: 'p1', status: 'Confirmed', discipline: 'Level', leagueName: 'L' },
      { id: 'b', playerId: 'p1', status: 'Rejected', discipline: 'Mixed', leagueName: 'L' },
      { id: 'c', playerId: 'p2', status: 'Pending', discipline: 'Level', leagueName: 'L' },
    ]);

    const rows = c['regsFor']('p1');
    expect(rows).toHaveLength(1);
    expect(rows[0].id).toBe('a');
  });

  it('toggleType flips Member <-> Visitor and persists', () => {
    const api = apiMock();
    const c = create(api);

    c['toggleType']({ playerId: 'p1', type: 'Member' });

    expect(api.updateLinkType).toHaveBeenCalledWith('club-1', 'p1', 'Visitor');
  });

  it('onTransferSearch loads candidates scoped to this club', () => {
    const api = apiMock({ transferCandidates: vi.fn(() => of([candidate()])) });
    const c = create(api);
    c['transferSearchControl'].setValue('jane');

    c['onTransferSearch']();

    expect(api.transferCandidates).toHaveBeenCalledWith('club-1', 'jane');
    expect(c['transferResults']()).toHaveLength(1);
  });

  it('isPicked matches on player + league + discipline, not name alone', () => {
    const c = create(apiMock());
    c['pickTransfer'](candidate());

    expect(c['isPicked'](candidate())).toBe(true);
    // Same person, different registration → not the picked one.
    expect(c['isPicked'](candidate({ leagueId: 'l2', discipline: 'Mixed' }))).toBe(false);
  });
});
