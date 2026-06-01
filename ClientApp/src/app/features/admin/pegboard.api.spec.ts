import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, afterEach, describe, expect, it } from 'vitest';
import { BoardView, PegboardApi } from './pegboard.api';

describe('PegboardApi', () => {
  let api: PegboardApi;
  let httpMock: HttpTestingController;

  const clubId = 'club-1';
  const sessionId = 'session-1';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(PegboardApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('openSession POSTs the name to the sessions URL', () => {
    api.openSession(clubId, 'Tuesday Club Night').subscribe();

    const req = httpMock.expectOne(`/api/clubs/${clubId}/pegboard/sessions`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Tuesday Club Night' });
    req.flush({ id: 'new-session' });
  });

  it('getBoard GETs the board URL and returns the view', () => {
    const view: BoardView = {
      session: { id: sessionId, clubId, name: 'Night', status: 'Open' },
      courts: [],
      attendees: [],
    };
    let result: BoardView | undefined;
    api.getBoard(clubId, sessionId).subscribe((b) => (result = b));

    const req = httpMock.expectOne(`/api/clubs/${clubId}/pegboard/sessions/${sessionId}/board`);
    expect(req.request.method).toBe('GET');
    req.flush(view);
    expect(result).toEqual(view);
  });

  it('startGame POSTs to the games URL with courtId as a query param', () => {
    api.startGame(clubId, sessionId, 'court-7', 'Singles', ['a1'], ['a2']).subscribe();

    const req = httpMock.expectOne(
      `/api/clubs/${clubId}/pegboard/sessions/${sessionId}/games?courtId=court-7`,
    );
    expect(req.request.method).toBe('POST');
    // courtId travels in the URL query string (verified by the expectOne URL above).
    expect(req.request.urlWithParams).toContain('courtId=court-7');
    expect(req.request.body).toEqual({ type: 'Singles', sideA: ['a1'], sideB: ['a2'] });
    req.flush({ id: 'game-1', makeupWarning: false });
  });

  it('finishGame POSTs the winner and score to the finish URL', () => {
    api.finishGame(clubId, sessionId, 'game-1', 'A', '21-15').subscribe();

    const req = httpMock.expectOne(
      `/api/clubs/${clubId}/pegboard/sessions/${sessionId}/games/game-1/finish`,
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ winnerSide: 'A', score: '21-15' });
    req.flush(null);
  });
});
