import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { ClubPlayersComponent } from './club-players.component';
import { PlayersApi, Player } from './players.api';

function apiMock(overrides: Partial<PlayersApi> = {}): PlayersApi {
  return {
    listClubPlayers: vi.fn(() => of([])),
    listClubRegistrations: vi.fn(() => of([])),
    listClubTransfers: vi.fn(() => of([])),
    updateLinkType: vi.fn(() => of(void 0)),
    unlinkPlayer: vi.fn(() => of(void 0)),
    searchPlayers: vi.fn(() => of([])),
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

  it('onCreateNameInput flags an exact-name duplicate (case-insensitive)', () => {
    const api = apiMock({
      searchPlayers: vi.fn(() =>
        of<Player[]>([
          { id: '1', fullName: 'Jane Smith', gender: 'Female' },
          { id: '2', fullName: 'jane smith', gender: 'Female' },
          { id: '3', fullName: 'Janet Smithers', gender: 'Female' },
        ]),
      ),
    });
    const c = create(api);
    c['createForm'].controls.fullName.setValue('Jane Smith');

    c['onCreateNameInput']();

    expect(c['createDup']()).toBe(2);
  });
});
