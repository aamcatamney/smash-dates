import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import PegboardBoardPage from './pegboard-board.page';
import { BoardView, PegboardApi } from './pegboard.api';
import { PlayersApi } from './players.api';

const board: BoardView = {
  session: { id: 'session-1', clubId: 'club-1', name: 'Tuesday Club Night', status: 'Open' },
  courts: [
    {
      id: 'court-1',
      label: 'Court 1',
      activeGame: {
        id: 'game-1',
        type: 'Singles',
        startedAt: '2026-01-01T00:00:00Z',
        players: [
          { attendanceId: 'a1', displayName: 'Alice', gender: 'Female', grade: 2, side: 'A' },
          { attendanceId: 'a2', displayName: 'Bob', gender: 'Male', grade: 3, side: 'B' },
        ],
      },
    },
    { id: 'court-2', label: 'Court 2', activeGame: null },
  ],
  attendees: [
    {
      id: 'a3',
      playerId: null,
      displayName: 'Carol',
      gender: 'Female',
      grade: 1,
      status: 'Waiting',
      waitingSince: '2026-05-31T10:00:00Z',
      gamesPlayed: 0,
      gamesWon: 0,
    },
    {
      id: 'a1',
      playerId: null,
      displayName: 'Alice',
      gender: 'Female',
      grade: 2,
      status: 'Playing',
      waitingSince: '2026-05-31T09:00:00Z',
      gamesPlayed: 1,
      gamesWon: 1,
    },
  ],
  canManage: true,
  clubName: 'Acme Badminton Club',
  clubShortCode: 'ACME',
};

function apiMock(overrides: Partial<PegboardApi> = {}): PegboardApi {
  return {
    stream: vi.fn(() => of(void 0)),
    getBoard: vi.fn(() => of(board)),
    addCourt: vi.fn(() => of({})),
    removeCourt: vi.fn(() => of(void 0)),
    addGuest: vi.fn(() => of({})),
    addPlayer: vi.fn(() => of({})),
    addVisitor: vi.fn(() => of({})),
    setAttendanceStatus: vi.fn(() => of(void 0)),
    removeAttendance: vi.fn(() => of(void 0)),
    suggest: vi.fn(() => of({ sideA: [], sideB: [] })),
    autoFill: vi.fn(() => of({ id: 'g' })),
    startGame: vi.fn(() => of({ id: 'g', makeupWarning: false })),
    finishGame: vi.fn(() => of(void 0)),
    cancelGame: vi.fn(() => of(void 0)),
    closeSession: vi.fn(() => of(void 0)),
    ...overrides,
  } as unknown as PegboardApi;
}

function create(api: PegboardApi) {
  TestBed.configureTestingModule({
    providers: [
      { provide: PegboardApi, useValue: api },
      {
        provide: PlayersApi,
        useValue: {
          listClubPlayers: vi.fn(() =>
            of([{ playerId: 'p1', fullName: 'Dana Existing', gender: 'Female', type: 'Member' }]),
          ),
        } as unknown as PlayersApi,
      },
      {
        provide: ActivatedRoute,
        useValue: {
          paramMap: of({
            get: (key: string) => (key === 'id' ? 'club-1' : 'session-1'),
          }),
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(PegboardBoardPage);
  fixture.detectChanges();
  return fixture;
}

describe('PegboardBoardPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('renders courts and waiting attendees from the mocked board', () => {
    const fixture = create(apiMock());
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Court 1');
    expect(text).toContain('Court 2');
    // Playing attendee shows in the active game; waiting attendee shows in the queue.
    expect(text).toContain('Alice');
    expect(text).toContain('Bob');
    expect(text).toContain('Carol');
  });

  it('clicking Add court calls addCourt', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['courtForm'].setValue({ label: 'Court 3' });
    c['onAddCourt']();

    expect(api.addCourt).toHaveBeenCalledWith('club-1', 'session-1', 'Court 3');
  });

  it('auto-fill calls autoFill with the open court and type', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['openFill'](board.courts[1]); // Court 2 (free)
    c['onAutoFill']('Doubles');

    expect(api.autoFill).toHaveBeenCalledWith('club-1', 'session-1', 'court-2', 'Doubles');
  });

  it('finishing a game calls finishGame', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['openFinish'](board.courts[0]);
    // Only the losing score is entered; the winner's (21) is derived.
    c['finishForm'].setValue({ winnerSide: 'A', loserScore: 15 });
    c['onFinish']();

    expect(api.finishGame).toHaveBeenCalledWith('club-1', 'session-1', 'game-1', 'A', '21-15');
  });

  it('adding an existing club player calls addPlayer', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['onAddExisting']({ playerId: 'p1', fullName: 'Dana', gender: 'Female', type: 'Member' });

    expect(api.addPlayer).toHaveBeenCalledWith('club-1', 'session-1', 'p1', null);
  });

  it('adding a new visitor calls addVisitor with the form values', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['visitorForm'].setValue({ name: 'Walk In', gender: 'Male', grade: '3' });
    c['onAddVisitor']();

    expect(api.addVisitor).toHaveBeenCalledWith('club-1', 'session-1', 'Walk In', 'Male', 3);
  });

  // Court controls live inside <app-court-card>; counting its buttons isolates host chrome from
  // the always-rendered (but closed) modals, whose titles would otherwise pollute a text match.
  function courtButtons(fixture: ReturnType<typeof create>): number {
    return (fixture.nativeElement as HTMLElement).querySelectorAll('app-court-card button').length;
  }

  it('shows host controls when canManage is true and the session is open', () => {
    const fixture = create(apiMock());
    const c = fixture.componentInstance as unknown as Record<string, any>;
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(c['isLive']()).toBe(true);
    expect(text).toContain('Close session');
    expect(courtButtons(fixture)).toBeGreaterThan(0); // Finish/Cancel + Fill on the free court
  });

  it('hides host controls and shows a read-only badge for a viewer', () => {
    const viewerBoard: BoardView = { ...board, canManage: false };
    const fixture = create(apiMock({ getBoard: vi.fn(() => of(viewerBoard)) }));
    const c = fixture.componentInstance as unknown as Record<string, any>;
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(c['isLive']()).toBe(false);
    expect(text).toContain('Viewing · read-only');
    expect(text).not.toContain('Close session');
    expect(courtButtons(fixture)).toBe(0);
    // Courts and players still render read-only.
    expect(text).toContain('Court 1');
    expect(text).toContain('Carol');
  });

  it('shows a closed-history badge and no controls for a closed session', () => {
    const closedBoard: BoardView = {
      ...board,
      session: { ...board.session, status: 'Closed' },
      canManage: true, // a host viewing history — still read-only
    };
    const fixture = create(apiMock({ getBoard: vi.fn(() => of(closedBoard)) }));
    const c = fixture.componentInstance as unknown as Record<string, any>;
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(c['isLive']()).toBe(false);
    expect(text).toContain('Closed · history');
    expect(text).not.toContain('Close session');
    expect(courtButtons(fixture)).toBe(0);
  });
});
