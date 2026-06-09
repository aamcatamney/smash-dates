import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { PegboardSessionsComponent } from './pegboard-sessions.component';
import { PegboardApi, SessionSummary } from './pegboard.api';
import { ClubsApi } from './clubs.api';

const base = {
  scheduledDate: null,
  startTime: null,
  durationMinutes: null,
  venueId: null,
  venueName: null,
  venueAddress: null,
  closedAt: null,
};

const sessions: SessionSummary[] = [
  {
    id: 's1',
    name: 'Tuesday Club Night',
    status: 'Open',
    openedAt: '2026-05-31T18:00:00Z',
    ...base,
  },
  {
    id: 's2',
    name: 'Last Friday',
    status: 'Closed',
    openedAt: '2026-05-23T18:00:00Z',
    ...base,
    closedAt: '2026-05-23T21:00:00Z',
  },
  {
    id: 's3',
    name: 'Next Tuesday',
    status: 'Scheduled',
    openedAt: null,
    ...base,
    scheduledDate: '2026-06-16',
    startTime: '19:30:00',
    venueName: 'Main Hall',
    venueAddress: '12 High St, Belfast',
  },
];

function apiMock(overrides: Partial<PegboardApi> = {}): PegboardApi {
  return {
    listSessions: vi.fn(() => of(sessions)),
    openSession: vi.fn(() => of({ id: 'new-1' })),
    scheduleSession: vi.fn(() => of({ id: 'sched-1' })),
    openScheduledSession: vi.fn(() => of({ id: 's3' })),
    updateScheduledSession: vi.fn(() => of(void 0)),
    deleteScheduledSession: vi.fn(() => of(void 0)),
    ...overrides,
  } as unknown as PegboardApi;
}

function create(api: PegboardApi) {
  TestBed.configureTestingModule({
    providers: [
      { provide: PegboardApi, useValue: api },
      { provide: ClubsApi, useValue: { listVenues: vi.fn(() => of([])) } as unknown as ClubsApi },
      // A wildcard route so the open-session navigation resolves cleanly in tests.
      provideRouter([{ path: '**', children: [] }]),
    ],
  });
  const fixture = TestBed.createComponent(PegboardSessionsComponent);
  fixture.componentRef.setInput('clubId', 'club-1');
  fixture.detectChanges();
  return fixture;
}

describe('PegboardSessionsComponent', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('renders sessions grouped by status', () => {
    const api = apiMock();
    const fixture = create(api);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(api.listSessions).toHaveBeenCalledWith('club-1');
    expect(text).toContain('Tuesday Club Night');
    expect(text).toContain('Last Friday');
    expect(text).toContain('Next Tuesday');
    expect(text).toContain('Main Hall');
  });

  it('Open-now calls openSession with the entered name', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['nowForm'].setValue({ name: 'Wednesday Night' });
    c['onOpenNowSubmit']();

    expect(api.openSession).toHaveBeenCalledWith('club-1', 'Wednesday Night');
  });

  it('schedules a future session with the form values', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['openScheduleDialog']();
    c['scheduleForm'].setValue({
      name: 'Future Night',
      scheduledDate: '2026-07-01',
      startTime: '19:30',
      durationMinutes: 120,
      venueId: '',
    });
    c['onSchedule']();

    expect(api.scheduleSession).toHaveBeenCalledWith('club-1', {
      name: 'Future Night',
      scheduledDate: '2026-07-01',
      startTime: '19:30:00',
      durationMinutes: 120,
      venueId: null,
    });
  });

  it('opens a scheduled session via openScheduledSession', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['onOpenNow'](sessions[2]);

    expect(api.openScheduledSession).toHaveBeenCalledWith('club-1', 's3');
  });

  it('on 409 shows a notice and refreshes the list', () => {
    const api = apiMock();
    (api.openSession as unknown as ReturnType<typeof vi.fn>).mockImplementation(() => ({
      subscribe: (o: { error: (e: unknown) => void }) => o.error({ status: 409 }),
    }));
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['nowForm'].setValue({ name: 'Dup' });
    c['onOpenNowSubmit']();

    expect(c['notice']()).toBeTruthy();
    // listSessions called once on init + once on refresh after 409.
    expect((api.listSessions as unknown as ReturnType<typeof vi.fn>).mock.calls.length).toBe(2);
  });
});
