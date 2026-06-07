import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ImportResult } from '../../shared/import-result';

export type DivisionGender = 'Mens' | 'Ladies' | 'Mixed';

export interface LeagueSummary {
  id: string;
  name: string;
  description: string | null;
  divisionCount: number;
  playerCount: number;
  activeSeasonName: string | null;
}

export type LeagueDetail = LeagueSummary;

export interface DivisionSummary {
  id: string;
  name: string;
  gender: DivisionGender;
  rank: number;
  rubbersPerMatch: number;
  winPoints: number;
  drawPoints: number;
  lossPoints: number;
}

export interface CreateLeagueRequest {
  name: string;
  description: string | null;
  firstLeagueAdminUserId: string;
}

export interface CreateDivisionRequest {
  name: string;
  gender: DivisionGender;
  rank: number;
  rubbersPerMatch: number;
  winPoints: number;
  drawPoints: number;
  lossPoints: number;
}

export interface LeagueAdminSummary {
  userId: string;
  email: string;
  displayName: string | null;
  grantedAt: string;
}

export interface UserLookup {
  id: string;
  email: string;
  displayName: string | null;
}

export type SeasonStatus = 'Draft' | 'Scheduling' | 'Proposed' | 'Active' | 'Closed';
export type WeekType = 'Level' | 'Mixed';

export interface WeekInput {
  startDate: string;
  endDate: string;
  weekType: WeekType;
}

export interface SeasonSummary {
  id: string;
  leagueId: string;
  name: string;
  startDate: string;
  endDate: string;
  status: SeasonStatus;
  schedulingError: string | null;
}

export interface SeasonDetail extends SeasonSummary {
  weeks: WeekInput[];
}

export interface CreateSeasonRequest {
  name: string;
  startDate: string;
  endDate: string;
  weeks: WeekInput[];
}

export interface SchedulingConfig {
  spreadWeight: number;
  legWeight: number;
  minGapDays: number;
  targetGapDays: number | null;
  courtsPerMatch: number;
}

export interface DivisionDiagnostic {
  divisionId: string;
  divisionName: string;
  teams: number;
  matchesRequired: number;
  matchesPlaced: number;
  eligibleWeeks: number;
}

export interface UnplacedPairing {
  divisionName: string;
  homeTeamName: string;
  awayTeamName: string;
}

export interface SchedulingDiagnostics {
  fullyPlaced: boolean;
  totalRequired: number;
  totalPlaced: number;
  divisions: DivisionDiagnostic[];
  unplaced: UnplacedPairing[];
}

export type MatchStatus = 'Proposed' | 'Confirmed' | 'Played' | 'Postponed' | 'Rejected';

export interface MatchSummary {
  id: string;
  divisionId: string;
  divisionName: string;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  venueId: string;
  venueName: string;
  matchDate: string;
  status: MatchStatus;
  homeAccepted: boolean;
  awayAccepted: boolean;
  homeScore: number | null;
  awayScore: number | null;
  isWalkover: boolean;
}

export interface StandingRow {
  teamId: string;
  teamName: string;
  played: number;
  won: number;
  drawn: number;
  lost: number;
  rubbersFor: number;
  rubbersAgainst: number;
  rubberDifference: number;
  points: number;
}

export interface DivisionTable {
  divisionId: string;
  divisionName: string;
  rows: StandingRow[];
}

export interface SeasonEntrySummary {
  id: string;
  seasonId: string;
  divisionId: string;
  divisionName: string;
  teamId: string;
  teamName: string;
  gender: DivisionGender;
}

export interface MembershipSummary {
  id: string;
  clubId: string;
  leagueId: string;
  status: 'Pending' | 'Accepted' | 'Declined' | 'Withdrawn' | 'Expelled';
  invitedAt: string;
  respondedAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class LeaguesApi {
  private readonly http = inject(HttpClient);

  list(): Observable<LeagueSummary[]> {
    return this.http.get<LeagueSummary[]>('/api/leagues');
  }

  get(id: string): Observable<LeagueDetail> {
    return this.http.get<LeagueDetail>(`/api/leagues/${id}`);
  }

  create(req: CreateLeagueRequest): Observable<LeagueSummary> {
    return this.http.post<LeagueSummary>('/api/leagues', req);
  }

  listDivisions(leagueId: string): Observable<DivisionSummary[]> {
    return this.http.get<DivisionSummary[]>(`/api/leagues/${leagueId}/divisions`);
  }

  createDivision(leagueId: string, req: CreateDivisionRequest): Observable<DivisionSummary> {
    return this.http.post<DivisionSummary>(`/api/leagues/${leagueId}/divisions`, req);
  }

  listAdmins(leagueId: string): Observable<LeagueAdminSummary[]> {
    return this.http.get<LeagueAdminSummary[]>(`/api/leagues/${leagueId}/admins`);
  }

  grantAdmin(leagueId: string, userId: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/admins`, { userId });
  }

  revokeAdmin(leagueId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`/api/leagues/${leagueId}/admins/${userId}`);
  }

  lookupUser(email: string): Observable<UserLookup> {
    const params = new HttpParams().set('email', email);
    return this.http.get<UserLookup>('/api/users/lookup', { params });
  }

  listMemberships(leagueId: string): Observable<MembershipSummary[]> {
    return this.http.get<MembershipSummary[]>(`/api/leagues/${leagueId}/memberships`);
  }

  invite(leagueId: string, clubId: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/memberships`, { clubId });
  }

  expel(leagueId: string, membershipId: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/memberships/${membershipId}/expel`, {});
  }

  listSeasons(leagueId: string): Observable<SeasonSummary[]> {
    return this.http.get<SeasonSummary[]>(`/api/leagues/${leagueId}/seasons`);
  }

  getSeason(leagueId: string, seasonId: string): Observable<SeasonDetail> {
    return this.http.get<SeasonDetail>(`/api/leagues/${leagueId}/seasons/${seasonId}`);
  }

  createSeason(leagueId: string, req: CreateSeasonRequest): Observable<SeasonSummary> {
    return this.http.post<SeasonSummary>(`/api/leagues/${leagueId}/seasons`, req);
  }

  replaceSeasonWeeks(leagueId: string, seasonId: string, weeks: WeekInput[]): Observable<void> {
    return this.http.put<void>(`/api/leagues/${leagueId}/seasons/${seasonId}/weeks`, { weeks });
  }

  deleteSeason(leagueId: string, seasonId: string): Observable<void> {
    return this.http.delete<void>(`/api/leagues/${leagueId}/seasons/${seasonId}`);
  }

  listSeasonEntries(leagueId: string, seasonId: string): Observable<SeasonEntrySummary[]> {
    return this.http.get<SeasonEntrySummary[]>(
      `/api/leagues/${leagueId}/seasons/${seasonId}/entries`,
    );
  }

  createSeasonEntry(
    leagueId: string,
    seasonId: string,
    teamId: string,
    divisionId: string,
  ): Observable<unknown> {
    return this.http.post(`/api/leagues/${leagueId}/seasons/${seasonId}/entries`, {
      teamId,
      divisionId,
    });
  }

  importSeasonEntries(leagueId: string, seasonId: string, csv: string): Observable<ImportResult> {
    return this.http.post<ImportResult>(
      `/api/leagues/${leagueId}/seasons/${seasonId}/entries/import`,
      { csv },
    );
  }

  deleteSeasonEntry(leagueId: string, seasonId: string, entryId: string): Observable<void> {
    return this.http.delete<void>(
      `/api/leagues/${leagueId}/seasons/${seasonId}/entries/${entryId}`,
    );
  }

  generateSchedule(leagueId: string, seasonId: string): Observable<{ matchCount: number }> {
    return this.http.post<{ matchCount: number }>(
      `/api/leagues/${leagueId}/seasons/${seasonId}/generate`,
      null,
    );
  }

  listMatches(leagueId: string, seasonId: string): Observable<MatchSummary[]> {
    return this.http.get<MatchSummary[]>(`/api/leagues/${leagueId}/seasons/${seasonId}/matches`);
  }

  forceConfirmMatch(matchId: string): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(`/api/matches/${matchId}/force-confirm`, null);
  }

  rerunSchedule(leagueId: string, seasonId: string): Observable<{ matchCount: number }> {
    return this.http.post<{ matchCount: number }>(
      `/api/leagues/${leagueId}/seasons/${seasonId}/rerun`,
      null,
    );
  }

  getSchedulingDiagnostics(leagueId: string, seasonId: string): Observable<SchedulingDiagnostics> {
    return this.http.get<SchedulingDiagnostics>(
      `/api/leagues/${leagueId}/seasons/${seasonId}/scheduling-diagnostics`,
    );
  }

  recordResult(
    matchId: string,
    homeScore: number,
    awayScore: number,
    playedOn: string,
  ): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(`/api/matches/${matchId}/result`, {
      homeScore,
      awayScore,
      playedOn,
    });
  }

  recordWalkover(matchId: string, winner: 'Home' | 'Away'): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(`/api/matches/${matchId}/walkover`, { winner });
  }

  listStandings(leagueId: string, seasonId: string): Observable<DivisionTable[]> {
    return this.http.get<DivisionTable[]>(`/api/leagues/${leagueId}/seasons/${seasonId}/standings`);
  }

  activateSeason(leagueId: string, seasonId: string): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(
      `/api/leagues/${leagueId}/seasons/${seasonId}/activate`,
      null,
    );
  }

  closeSeason(leagueId: string, seasonId: string): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(
      `/api/leagues/${leagueId}/seasons/${seasonId}/close`,
      null,
    );
  }

  postponeMatch(matchId: string): Observable<{ status: string }> {
    return this.http.post<{ status: string }>(`/api/matches/${matchId}/postpone`, null);
  }

  getSchedulingConfig(leagueId: string): Observable<SchedulingConfig> {
    return this.http.get<SchedulingConfig>(`/api/leagues/${leagueId}/scheduling-config`);
  }

  updateSchedulingConfig(leagueId: string, config: SchedulingConfig): Observable<void> {
    return this.http.patch<void>(`/api/leagues/${leagueId}/scheduling-config`, config);
  }
}
