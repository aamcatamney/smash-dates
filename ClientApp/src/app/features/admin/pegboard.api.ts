import { HttpClient } from '@angular/common/http';
import { Injectable, NgZone, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Gender } from './players.api';

export type PegSessionStatus = 'Scheduled' | 'Open' | 'Closed';
export type AttendanceStatus = 'Waiting' | 'Playing' | 'Resting' | 'Left';
export type GameType = 'Singles' | 'Doubles' | 'Mixed' | 'Funny';
export type GameSide = 'A' | 'B';

export interface SessionSummary {
  id: string;
  name: string;
  status: PegSessionStatus;
  // Planning fields, set on Scheduled sessions (date required; the rest optional).
  scheduledDate: string | null;
  startTime: string | null;
  durationMinutes: number | null;
  venueId: string | null;
  venueName: string | null;
  venueAddress: string | null;
  openedAt: string | null;
  closedAt: string | null;
}

// Create/edit payload for a scheduled session.
export interface ScheduleInput {
  name: string;
  scheduledDate: string;
  startTime: string | null;
  durationMinutes: number | null;
  venueId: string | null;
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
  // ISO timestamp the game started — drives the live court timer.
  startedAt: string;
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
  // True when the requester may run this session; drives host-vs-viewer chrome.
  canManage: boolean;
  // Club identity for the board header.
  clubName: string;
  clubShortCode: string;
}

export interface FillSuggestion {
  sideA: string[];
  sideB: string[];
}

// Closed-session per-player breakdown (history). Court time is exact; waiting time is derived.
export interface SessionMatch {
  gameId: string;
  type: GameType;
  startedAt: string;
  endedAt: string;
  durationSeconds: number;
  side: GameSide;
  won: boolean;
  score: string | null;
  partners: string[];
  opponents: string[];
}
export interface SessionPlayerSummary {
  attendanceId: string;
  displayName: string;
  courtSeconds: number;
  waitingSeconds: number;
  gamesPlayed: number;
  gamesWon: number;
  matches: SessionMatch[];
}
export interface SessionSummaryView {
  sessionId: string;
  status: PegSessionStatus;
  openedAt: string | null;
  closedAt: string | null;
  players: SessionPlayerSummary[];
}

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
  // Plan a session ahead of time (Scheduled). Opened later via openScheduledSession.
  scheduleSession(clubId: string, input: ScheduleInput): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base(clubId)}/scheduled`, input);
  }
  openScheduledSession(clubId: string, sessionId: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.base(clubId)}/${sessionId}/open`, null);
  }
  updateScheduledSession(
    clubId: string,
    sessionId: string,
    input: ScheduleInput,
  ): Observable<void> {
    return this.http.patch<void>(`${this.base(clubId)}/${sessionId}`, input);
  }
  deleteScheduledSession(clubId: string, sessionId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(clubId)}/${sessionId}`);
  }
  closeSession(clubId: string, sessionId: string): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/close`, null);
  }
  getBoard(clubId: string, sessionId: string): Observable<BoardView> {
    return this.http.get<BoardView>(`${this.base(clubId)}/${sessionId}/board`);
  }
  // Closed-session breakdown: per-player matches and court-vs-waiting time.
  getSessionSummary(clubId: string, sessionId: string): Observable<SessionSummaryView> {
    return this.http.get<SessionSummaryView>(`${this.base(clubId)}/${sessionId}/summary`);
  }

  addCourt(clubId: string, sessionId: string, label: string): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/courts`, { label });
  }
  removeCourt(clubId: string, sessionId: string, courtId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(clubId)}/${sessionId}/courts/${courtId}`);
  }
  addGuest(
    clubId: string,
    sessionId: string,
    guestName: string,
    gender: Gender,
    grade: number | null,
  ): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/attendances`, {
      guestName,
      gender,
      grade,
    });
  }
  addPlayer(
    clubId: string,
    sessionId: string,
    playerId: string,
    grade: number | null,
  ): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/attendances`, { playerId, grade });
  }
  // Register a walk-in as a real Visitor player on the club, then add them to the board.
  addVisitor(
    clubId: string,
    sessionId: string,
    fullName: string,
    gender: Gender,
    grade: number | null,
  ): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/attendances`, {
      newVisitor: { fullName, gender, grade },
    });
  }
  setAttendanceStatus(
    clubId: string,
    sessionId: string,
    attendanceId: string,
    status: AttendanceStatus,
  ): Observable<void> {
    return this.http.patch<void>(`${this.base(clubId)}/${sessionId}/attendances/${attendanceId}`, {
      status,
    });
  }
  removeAttendance(clubId: string, sessionId: string, attendanceId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(clubId)}/${sessionId}/attendances/${attendanceId}`);
  }
  suggest(clubId: string, sessionId: string, type: GameType): Observable<FillSuggestion> {
    return this.http.post<FillSuggestion>(`${this.base(clubId)}/${sessionId}/suggest`, { type });
  }
  // Auto-rotate: the board picks a valid lineup and starts the game in one call.
  autoFill(
    clubId: string,
    sessionId: string,
    courtId: string,
    type: GameType,
  ): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(
      `${this.base(clubId)}/${sessionId}/courts/${courtId}/auto-fill`,
      { type },
    );
  }
  startGame(
    clubId: string,
    sessionId: string,
    courtId: string,
    type: GameType,
    sideA: string[],
    sideB: string[],
  ): Observable<{ id: string; makeupWarning: boolean }> {
    return this.http.post<{ id: string; makeupWarning: boolean }>(
      `${this.base(clubId)}/${sessionId}/games?courtId=${courtId}`,
      { type, sideA, sideB },
    );
  }
  finishGame(
    clubId: string,
    sessionId: string,
    gameId: string,
    winnerSide: GameSide,
    score: string | null,
  ): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/games/${gameId}/finish`, {
      winnerSide,
      score,
    });
  }
  cancelGame(clubId: string, sessionId: string, gameId: string): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/games/${gameId}/cancel`, null);
  }

  // SSE: emits once on connect and on each board-changed event. Re-fetch the board on each emission.
  stream(clubId: string, sessionId: string): Observable<void> {
    return new Observable<void>((subscriber) => {
      const es = new EventSource(`${this.base(clubId)}/${sessionId}/stream`, {
        withCredentials: true,
      });
      const onMsg = () => this.zone.run(() => subscriber.next());
      es.addEventListener('board-changed', onMsg);
      es.onerror = () => {
        /* EventSource auto-reconnects; ignore transient errors */
      };
      return () => es.close();
    });
  }
}
