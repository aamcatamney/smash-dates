import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type DivisionGender = 'Mens' | 'Ladies' | 'Mixed';

export interface LeagueSummary {
  id: string;
  name: string;
  description: string | null;
}

export interface LeagueDetail extends LeagueSummary {
  createdBy: string;
}

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
}
