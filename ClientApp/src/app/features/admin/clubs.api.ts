import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ImportResult } from '../../shared/import-result';

export interface ClubSummary {
  id: string;
  name: string;
  shortCode: string;
  contactEmail: string;
  notes: string | null;
}

export type ClubDetail = ClubSummary;

export interface ClubAdminSummary {
  userId: string;
  email: string;
  displayName: string | null;
  grantedAt: string;
}

export type MembershipStatus = 'Pending' | 'Accepted' | 'Declined' | 'Withdrawn' | 'Expelled';

export interface MembershipSummary {
  id: string;
  clubId: string;
  leagueId: string;
  status: MembershipStatus;
  invitedAt: string;
  respondedAt: string | null;
}

export interface CreateClubRequest {
  name: string;
  shortCode: string;
  contactEmail: string;
  notes: string | null;
  firstClubAdminUserId: string;
}

export interface UpdateClubRequest {
  name: string;
  shortCode: string;
  contactEmail: string;
  notes: string | null;
}

export type Gender = 'Mens' | 'Ladies' | 'Mixed';

export interface TeamSummary {
  id: string;
  clubId: string;
  name: string;
  gender: Gender;
}

export interface VenueSummary {
  id: string;
  clubId: string;
  name: string;
  courts: number;
  maxConcurrentMatches: number;
}

export type MatchStatus = 'Proposed' | 'Confirmed' | 'Played' | 'Postponed' | 'Rejected';

export interface ClubMatch {
  id: string;
  divisionName: string;
  homeTeamId: string;
  homeTeamName: string;
  awayTeamId: string;
  awayTeamName: string;
  venueName: string;
  matchDate: string;
  status: MatchStatus;
  homeAccepted: boolean;
  awayAccepted: boolean;
  homeScore: number | null;
  awayScore: number | null;
  isWalkover: boolean;
}

export type BlockedDateScope = 'Club' | 'Venue' | 'Team';

export interface BlockedDateSummary {
  id: string;
  clubId: string;
  scope: BlockedDateScope;
  venueId: string | null;
  teamId: string | null;
  startDate: string;
  endDate: string;
  reason: string;
}

export interface CreateBlockedDateRequest {
  scope: BlockedDateScope;
  venueId?: string | null;
  teamId?: string | null;
  startDate: string;
  endDate: string;
  reason: string;
}

@Injectable({ providedIn: 'root' })
export class ClubsApi {
  private readonly http = inject(HttpClient);

  list(): Observable<ClubSummary[]> {
    return this.http.get<ClubSummary[]>('/api/clubs');
  }

  get(id: string): Observable<ClubDetail> {
    return this.http.get<ClubDetail>(`/api/clubs/${id}`);
  }

  create(req: CreateClubRequest): Observable<ClubSummary> {
    return this.http.post<ClubSummary>('/api/clubs', req);
  }

  importClubs(csv: string): Observable<ImportResult> {
    return this.http.post<ImportResult>('/api/clubs/import', { csv });
  }

  importTeams(clubId: string, csv: string): Observable<ImportResult> {
    return this.http.post<ImportResult>(`/api/clubs/${clubId}/teams/import`, { csv });
  }

  importVenues(clubId: string, csv: string): Observable<ImportResult> {
    return this.http.post<ImportResult>(`/api/clubs/${clubId}/venues/import`, { csv });
  }

  update(id: string, req: UpdateClubRequest): Observable<void> {
    return this.http.patch<void>(`/api/clubs/${id}`, req);
  }

  listAdmins(clubId: string): Observable<ClubAdminSummary[]> {
    return this.http.get<ClubAdminSummary[]>(`/api/clubs/${clubId}/admins`);
  }

  grantAdmin(clubId: string, userId: string): Observable<void> {
    return this.http.post<void>(`/api/clubs/${clubId}/admins`, { userId });
  }

  revokeAdmin(clubId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`/api/clubs/${clubId}/admins/${userId}`);
  }

  listMemberships(clubId: string): Observable<MembershipSummary[]> {
    return this.http.get<MembershipSummary[]>(`/api/clubs/${clubId}/memberships`);
  }

  acceptMembership(leagueId: string, membershipId: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/memberships/${membershipId}/accept`, {});
  }

  declineMembership(leagueId: string, membershipId: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/memberships/${membershipId}/decline`, {});
  }

  withdrawMembership(leagueId: string, membershipId: string): Observable<void> {
    return this.http.post<void>(
      `/api/leagues/${leagueId}/memberships/${membershipId}/withdraw`,
      {},
    );
  }

  listTeams(clubId: string): Observable<TeamSummary[]> {
    return this.http.get<TeamSummary[]>(`/api/clubs/${clubId}/teams`);
  }

  createTeam(clubId: string, name: string, gender: Gender): Observable<TeamSummary> {
    return this.http.post<TeamSummary>(`/api/clubs/${clubId}/teams`, { name, gender });
  }

  renameTeam(clubId: string, teamId: string, name: string): Observable<void> {
    return this.http.patch<void>(`/api/clubs/${clubId}/teams/${teamId}`, { name });
  }

  deleteTeam(clubId: string, teamId: string): Observable<void> {
    return this.http.delete<void>(`/api/clubs/${clubId}/teams/${teamId}`);
  }

  listVenues(clubId: string): Observable<VenueSummary[]> {
    return this.http.get<VenueSummary[]>(`/api/clubs/${clubId}/venues`);
  }

  createVenue(
    clubId: string,
    name: string,
    courts: number,
    maxConcurrentMatches: number,
  ): Observable<VenueSummary> {
    return this.http.post<VenueSummary>(`/api/clubs/${clubId}/venues`, {
      name,
      courts,
      maxConcurrentMatches,
    });
  }

  updateVenue(
    clubId: string,
    venueId: string,
    name: string,
    courts: number,
    maxConcurrentMatches: number,
  ): Observable<void> {
    return this.http.patch<void>(`/api/clubs/${clubId}/venues/${venueId}`, {
      name,
      courts,
      maxConcurrentMatches,
    });
  }

  deleteVenue(clubId: string, venueId: string): Observable<void> {
    return this.http.delete<void>(`/api/clubs/${clubId}/venues/${venueId}`);
  }

  listBlockedDates(clubId: string): Observable<BlockedDateSummary[]> {
    return this.http.get<BlockedDateSummary[]>(`/api/clubs/${clubId}/blocked-dates`);
  }

  createBlockedDate(clubId: string, req: CreateBlockedDateRequest): Observable<BlockedDateSummary> {
    return this.http.post<BlockedDateSummary>(`/api/clubs/${clubId}/blocked-dates`, req);
  }

  deleteBlockedDate(clubId: string, id: string): Observable<void> {
    return this.http.delete<void>(`/api/clubs/${clubId}/blocked-dates/${id}`);
  }

  listMatches(clubId: string): Observable<ClubMatch[]> {
    return this.http.get<ClubMatch[]>(`/api/clubs/${clubId}/matches`);
  }

  acceptMatch(matchId: string): Observable<unknown> {
    return this.http.post(`/api/matches/${matchId}/accept`, null);
  }

  rejectMatch(matchId: string): Observable<unknown> {
    return this.http.post(`/api/matches/${matchId}/reject`, null);
  }

  recordResult(
    matchId: string,
    homeScore: number,
    awayScore: number,
    playedOn: string,
  ): Observable<unknown> {
    return this.http.post(`/api/matches/${matchId}/result`, { homeScore, awayScore, playedOn });
  }

  recordWalkover(matchId: string, winner: 'Home' | 'Away'): Observable<unknown> {
    return this.http.post(`/api/matches/${matchId}/walkover`, { winner });
  }
}
