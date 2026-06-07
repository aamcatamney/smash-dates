import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import PublicLeaguesPage from './public-leagues.page';
import { PublicApi } from './public.api';

function apiMock(): PublicApi {
  return {
    listLeagues: vi.fn(() =>
      of([
        { id: 'l1', name: 'North League', description: 'Top flight' },
        { id: 'l2', name: 'South League', description: null },
      ]),
    ),
  } as unknown as PublicApi;
}

describe('PublicLeaguesPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('lists leagues from the public API', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: PublicApi, useValue: apiMock() }, provideRouter([])],
    });
    const fixture = TestBed.createComponent(PublicLeaguesPage);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('North League');
    expect(text).toContain('Top flight');
    expect(text).toContain('South League');
  });
});
