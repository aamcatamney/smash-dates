import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

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
    return this.http.post<void>(`/api/leagues/${leagueId}/memberships/${membershipId}/withdraw`, {});
  }
}
