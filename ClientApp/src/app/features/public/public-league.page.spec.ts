import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import PublicLeaguePage from './public-league.page';
import { PublicApi } from './public.api';

function apiMock(overrides: Partial<PublicApi> = {}): PublicApi {
  return {
    getLeague: vi.fn(() =>
      of({
        id: 'l1',
        name: 'North League',
        description: null,
        seasons: [
          { id: 's1', name: '2024/25', startDate: '2024-09-01', endDate: '2025-04-01', status: 'Closed' },
          { id: 's2', name: '2025/26', startDate: '2025-09-01', endDate: '2026-04-01', status: 'Active' },
        ],
      }),
    ),
    getStandings: vi.fn(() =>
      of([
        {
          divisionId: 'd1',
          divisionName: 'Mens 1',
          rows: [
            { teamId: 't1', teamName: 'Acme 1', played: 1, won: 1, drawn: 0, lost: 0, rubbersFor: 9, rubbersAgainst: 0, rubberDifference: 9, points: 2 },
          ],
        },
      ]),
    ),
    getFixtures: vi.fn(() =>
      of([
        {
          id: 'm1',
          divisionName: 'Mens 1',
          homeTeamName: 'Acme 1',
          awayTeamName: 'Beta 1',
          venueName: 'Acme Hall',
          matchDate: '2025-09-03',
          status: 'Played',
          homeScore: 9,
          awayScore: 0,
          isWalkover: false,
        },
      ]),
    ),
    ...overrides,
  } as unknown as PublicApi;
}

function create(api: PublicApi) {
  TestBed.configureTestingModule({
    providers: [
      { provide: PublicApi, useValue: api },
      provideRouter([]),
      // After provideRouter so this mock wins the ActivatedRoute token (last provider wins).
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'l1' } } } },
    ],
  });
  const fixture = TestBed.createComponent(PublicLeaguePage);
  fixture.detectChanges();
  return fixture;
}

describe('PublicLeaguePage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('defaults to the Active season and renders standings + fixtures', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(c['selectedSeasonId']()).toBe('s2'); // the Active season, not the first listed
    expect(api.getStandings).toHaveBeenCalledWith('l1', 's2');
    expect(text).toContain('North League');
    expect(text).toContain('Mens 1');
    expect(text).toContain('Acme 1');
    expect(text).toContain('Acme 1 v Beta 1');
    expect(text).toContain('9–0');
  });
});
