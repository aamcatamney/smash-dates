import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type DivisionGender = 'Mens' | 'Ladies' | 'Mixed';

export interface LeagueSummary {
  id: string;
  name: string;
  description: string | null;
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
}
