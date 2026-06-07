import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

// Anonymous, read-only public view (no login). Mirrors the /api/public endpoints.

export interface PublicLeague {
  id: string;
  name: string;
  description: string | null;
}

export interface PublicSeason {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: string;
}

export interface PublicLeagueDetail extends PublicLeague {
  seasons: PublicSeason[];
}

export interface PublicStandingRow {
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

export interface PublicStandingsTable {
  divisionId: string;
  divisionName: string;
  rows: PublicStandingRow[];
}

export interface PublicFixture {
  id: string;
  divisionName: string;
  homeTeamName: string;
  awayTeamName: string;
  venueName: string;
  matchDate: string;
  status: string;
  homeScore: number | null;
  awayScore: number | null;
  isWalkover: boolean;
}

@Injectable({ providedIn: 'root' })
export class PublicApi {
  private readonly http = inject(HttpClient);

  listLeagues(): Observable<PublicLeague[]> {
    return this.http.get<PublicLeague[]>('/api/public/leagues');
  }

  getLeague(leagueId: string): Observable<PublicLeagueDetail> {
    return this.http.get<PublicLeagueDetail>(`/api/public/leagues/${leagueId}`);
  }

  getStandings(leagueId: string, seasonId: string): Observable<PublicStandingsTable[]> {
    return this.http.get<PublicStandingsTable[]>(
      `/api/public/leagues/${leagueId}/seasons/${seasonId}/standings`,
    );
  }

  getFixtures(leagueId: string, seasonId: string): Observable<PublicFixture[]> {
    return this.http.get<PublicFixture[]>(
      `/api/public/leagues/${leagueId}/seasons/${seasonId}/fixtures`,
    );
  }
}
