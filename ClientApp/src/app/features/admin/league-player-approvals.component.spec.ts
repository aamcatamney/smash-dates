import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { LeaguePlayerApprovalsComponent } from './league-player-approvals.component';
import { PlayersApi } from './players.api';

function apiMock(overrides: Partial<PlayersApi> = {}): PlayersApi {
  return {
    listLeagueRegistrations: vi.fn(() => of([])),
    listLeagueTransfers: vi.fn(() => of([])),
    confirmRegistration: vi.fn(() => of(void 0)),
    rejectRegistration: vi.fn(() => of(void 0)),
    leagueApproveTransfer: vi.fn(() => of(void 0)),
    leagueRejectTransfer: vi.fn(() => of(void 0)),
    ...overrides,
  } as unknown as PlayersApi;
}

function create(api: PlayersApi) {
  TestBed.configureTestingModule({ providers: [{ provide: PlayersApi, useValue: api }] });
  const fixture = TestBed.createComponent(LeaguePlayerApprovalsComponent);
  fixture.componentRef.setInput('leagueId', 'league-1');
  return fixture.componentInstance as unknown as Record<string, any>;
}

describe('LeaguePlayerApprovalsComponent', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('confirmReg confirms the registration for this league', () => {
    const api = apiMock();
    const c = create(api);

    c['confirmReg']({ id: 'reg-1' });

    expect(api.confirmRegistration).toHaveBeenCalledWith('league-1', 'reg-1');
  });

  it('approveTransfer approves the transfer for this league', () => {
    const api = apiMock();
    const c = create(api);

    c['approveTransfer']({ id: 't-1' });

    expect(api.leagueApproveTransfer).toHaveBeenCalledWith('league-1', 't-1');
  });
});
