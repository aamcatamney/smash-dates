import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import PegboardBoardPage from './pegboard-board.page';
import { BoardView, PegboardApi } from './pegboard.api';

const board: BoardView = {
  session: { id: 'session-1', clubId: 'club-1', name: 'Tuesday Club Night', status: 'Open' },
  courts: [
    {
      id: 'court-1',
      label: 'Court 1',
      activeGame: {
        id: 'game-1',
        type: 'Singles',
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
};

function apiMock(overrides: Partial<PegboardApi> = {}): PegboardApi {
  return {
    stream: vi.fn(() => of(void 0)),
    getBoard: vi.fn(() => of(board)),
    addCourt: vi.fn(() => of({})),
    removeCourt: vi.fn(() => of(void 0)),
    addGuest: vi.fn(() => of({})),
    setAttendanceStatus: vi.fn(() => of(void 0)),
    removeAttendance: vi.fn(() => of(void 0)),
    suggest: vi.fn(() => of({ sideA: [], sideB: [] })),
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

  it('finishing a game calls finishGame', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['openFinish'](board.courts[0]);
    c['finishForm'].setValue({ winnerSide: 'A', score: '21-15' });
    c['onFinish']();

    expect(api.finishGame).toHaveBeenCalledWith('club-1', 'session-1', 'game-1', 'A', '21-15');
  });
});
