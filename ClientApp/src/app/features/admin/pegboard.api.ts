import { HttpClient } from '@angular/common/http';
import { Injectable, NgZone, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Gender } from './players.api';

export type PegSessionStatus = 'Open' | 'Closed';
export type AttendanceStatus = 'Waiting' | 'Playing' | 'Resting' | 'Left';
export type GameType = 'Singles' | 'Doubles' | 'Mixed' | 'Funny';
export type GameSide = 'A' | 'B';

export interface SessionSummary {
  id: string;
  name: string;
  status: PegSessionStatus;
  openedAt: string;
  closedAt: string | null;
}

export interface BoardGamePlayer {
  attendanceId: string;
  displayName: string;
  gender: Gender;
  grade: number | null;
  side: GameSide;
}
export interface BoardGame {
  id: string;
  type: GameType;
  players: BoardGamePlayer[];
}
export interface BoardCourt {
  id: string;
  label: string;
  activeGame: BoardGame | null;
}
export interface BoardAttendee {
  id: string;
  playerId: string | null;
  displayName: string;
  gender: Gender;
  grade: number | null;
  status: AttendanceStatus;
  waitingSince: string;
  gamesPlayed: number;
  gamesWon: number;
}
export interface BoardView {
  session: { id: string; clubId: string; name: string; status: PegSessionStatus };
  courts: BoardCourt[];
  attendees: BoardAttendee[];
}

export interface FillSuggestion { sideA: string[]; sideB: string[]; }

@Injectable({ providedIn: 'root' })
export class PegboardApi {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);

  private base(clubId: string): string {
    return `/api/clubs/${clubId}/pegboard/sessions`;
  }

  listSessions(clubId: string): Observable<SessionSummary[]> {
    return this.http.get<SessionSummary[]>(this.base(clubId));
  }
  openSession(clubId: string, name: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.base(clubId), { name });
  }
  closeSession(clubId: string, sessionId: string): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/close`, null);
  }
  getBoard(clubId: string, sessionId: string): Observable<BoardView> {
    return this.http.get<BoardView>(`${this.base(clubId)}/${sessionId}/board`);
  }

  addCourt(clubId: string, sessionId: string, label: string): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/courts`, { label });
  }
  removeCourt(clubId: string, sessionId: string, courtId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(clubId)}/${sessionId}/courts/${courtId}`);
  }
  addGuest(clubId: string, sessionId: string, guestName: string, gender: Gender, grade: number | null): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/attendances`, { guestName, gender, grade });
  }
  addPlayer(clubId: string, sessionId: string, playerId: string, grade: number | null): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/attendances`, { playerId, grade });
  }
  setAttendanceStatus(clubId: string, sessionId: string, attendanceId: string, status: AttendanceStatus): Observable<void> {
    return this.http.patch<void>(`${this.base(clubId)}/${sessionId}/attendances/${attendanceId}`, { status });
  }
  removeAttendance(clubId: string, sessionId: string, attendanceId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(clubId)}/${sessionId}/attendances/${attendanceId}`);
  }
  suggest(clubId: string, sessionId: string, type: GameType): Observable<FillSuggestion> {
    return this.http.post<FillSuggestion>(`${this.base(clubId)}/${sessionId}/suggest`, { type });
  }
  startGame(clubId: string, sessionId: string, courtId: string, type: GameType, sideA: string[], sideB: string[]): Observable<{ id: string; makeupWarning: boolean }> {
    return this.http.post<{ id: string; makeupWarning: boolean }>(
      `${this.base(clubId)}/${sessionId}/games?courtId=${courtId}`, { type, sideA, sideB });
  }
  finishGame(clubId: string, sessionId: string, gameId: string, winnerSide: GameSide, score: string | null): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/games/${gameId}/finish`, { winnerSide, score });
  }
  cancelGame(clubId: string, sessionId: string, gameId: string): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/games/${gameId}/cancel`, null);
  }

  // SSE: emits once on connect and on each board-changed event. Re-fetch the board on each emission.
  stream(clubId: string, sessionId: string): Observable<void> {
    return new Observable<void>((subscriber) => {
      const es = new EventSource(`${this.base(clubId)}/${sessionId}/stream`, { withCredentials: true });
      const onMsg = () => this.zone.run(() => subscriber.next());
      es.addEventListener('board-changed', onMsg);
      es.onerror = () => { /* EventSource auto-reconnects; ignore transient errors */ };
      return () => es.close();
    });
  }
}
