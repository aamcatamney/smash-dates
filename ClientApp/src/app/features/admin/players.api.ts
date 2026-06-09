import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ImportResult } from '../../shared/import-result';

export type Gender = 'Male' | 'Female';
export type PlayerClubType = 'Member' | 'Visitor';
export type Discipline = 'Level' | 'Mixed';
export type RegistrationStatus = 'Pending' | 'Confirmed' | 'Rejected';
export type TransferStatus = 'Pending' | 'Completed' | 'Rejected';

export interface Player {
  id: string;
  fullName: string;
  gender: Gender;
}

export interface PlayerLink {
  playerId: string;
  fullName: string;
  gender: Gender;
  type: PlayerClubType;
  grade: number | null;
}

export interface Registration {
  id: string;
  playerId: string;
  playerName: string;
  gender: Gender;
  clubId: string;
  clubShortCode: string;
  leagueId: string;
  leagueName: string;
  discipline: Discipline;
  status: RegistrationStatus;
}

export interface SquadMember {
  playerId: string;
  fullName: string;
  gender: Gender;
}

// A Confirmed registration a club may transfer in — scoped to the receiving club's leagues.
export interface TransferCandidate {
  playerId: string;
  fullName: string;
  gender: Gender;
  leagueId: string;
  leagueName: string;
  discipline: Discipline;
  currentClubShortCode: string;
}

export interface Transfer {
  id: string;
  playerId: string;
  playerName: string;
  discipline: Discipline;
  fromClubId: string;
  fromShortCode: string;
  toClubId: string;
  toShortCode: string;
  leagueId: string;
  leagueName: string;
  status: TransferStatus;
  releasingApproved: boolean;
  leagueApproved: boolean;
}

@Injectable({ providedIn: 'root' })
export class PlayersApi {
  private readonly http = inject(HttpClient);

  // --- club affiliations ---
  listClubPlayers(clubId: string): Observable<PlayerLink[]> {
    return this.http.get<PlayerLink[]>(`/api/clubs/${clubId}/players`);
  }

  importClubPlayers(clubId: string, csv: string): Observable<ImportResult> {
    return this.http.post<ImportResult>(`/api/clubs/${clubId}/players/import`, { csv });
  }

  addNewPlayer(
    clubId: string,
    fullName: string,
    gender: Gender,
    type: PlayerClubType,
  ): Observable<Player> {
    return this.http.post<Player>(`/api/clubs/${clubId}/players`, { fullName, gender, type });
  }

  updateLinkType(clubId: string, playerId: string, type: PlayerClubType): Observable<void> {
    return this.http.patch<void>(`/api/clubs/${clubId}/players/${playerId}`, { type });
  }

  // Edit name + stored grade (gender immutable). Player is global: a rename shows everywhere.
  updatePlayerDetails(
    clubId: string,
    playerId: string,
    fullName: string,
    grade: number | null,
  ): Observable<void> {
    return this.http.patch<void>(`/api/clubs/${clubId}/players/${playerId}/details`, {
      fullName,
      grade,
    });
  }

  unlinkPlayer(clubId: string, playerId: string): Observable<void> {
    return this.http.delete<void>(`/api/clubs/${clubId}/players/${playerId}`);
  }

  // --- registrations ---
  listClubRegistrations(clubId: string): Observable<Registration[]> {
    return this.http.get<Registration[]>(`/api/clubs/${clubId}/registrations`);
  }

  listLeagueRegistrations(leagueId: string): Observable<Registration[]> {
    return this.http.get<Registration[]>(`/api/leagues/${leagueId}/registrations`);
  }

  requestRegistration(
    clubId: string,
    playerId: string,
    leagueId: string,
    discipline: Discipline,
  ): Observable<unknown> {
    return this.http.post(`/api/clubs/${clubId}/players/${playerId}/registrations`, {
      leagueId,
      discipline,
    });
  }

  confirmRegistration(leagueId: string, id: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/registrations/${id}/confirm`, null);
  }

  rejectRegistration(leagueId: string, id: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/registrations/${id}/reject`, null);
  }

  // --- transfers ---
  transferCandidates(clubId: string, query: string): Observable<TransferCandidate[]> {
    return this.http.get<TransferCandidate[]>(
      `/api/clubs/${clubId}/transfers/candidates?search=${encodeURIComponent(query)}`,
    );
  }

  listClubTransfers(clubId: string): Observable<Transfer[]> {
    return this.http.get<Transfer[]>(`/api/clubs/${clubId}/transfers`);
  }

  listLeagueTransfers(leagueId: string): Observable<Transfer[]> {
    return this.http.get<Transfer[]>(`/api/leagues/${leagueId}/transfers`);
  }

  openTransfer(
    clubId: string,
    playerId: string,
    leagueId: string,
    discipline: Discipline,
  ): Observable<unknown> {
    return this.http.post(`/api/clubs/${clubId}/transfers`, { playerId, leagueId, discipline });
  }

  clubApproveTransfer(clubId: string, id: string): Observable<void> {
    return this.http.post<void>(`/api/clubs/${clubId}/transfers/${id}/approve`, null);
  }

  clubRejectTransfer(clubId: string, id: string): Observable<void> {
    return this.http.post<void>(`/api/clubs/${clubId}/transfers/${id}/reject`, null);
  }

  leagueApproveTransfer(leagueId: string, id: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/transfers/${id}/approve`, null);
  }

  leagueRejectTransfer(leagueId: string, id: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/transfers/${id}/reject`, null);
  }

  // --- team squads ---
  listSquad(clubId: string, teamId: string): Observable<SquadMember[]> {
    return this.http.get<SquadMember[]>(`/api/clubs/${clubId}/teams/${teamId}/players`);
  }

  addToSquad(clubId: string, teamId: string, playerId: string): Observable<unknown> {
    return this.http.post(`/api/clubs/${clubId}/teams/${teamId}/players`, { playerId });
  }

  removeFromSquad(clubId: string, teamId: string, playerId: string): Observable<void> {
    return this.http.delete<void>(`/api/clubs/${clubId}/teams/${teamId}/players/${playerId}`);
  }
}
