import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { describe, beforeEach, expect, it, vi } from 'vitest';
import { PegboardSessionsComponent } from './pegboard-sessions.component';
import { PegboardApi, SessionSummary } from './pegboard.api';

const sessions: SessionSummary[] = [
  { id: 's1', name: 'Tuesday Club Night', status: 'Open', openedAt: '2026-05-31T18:00:00Z', closedAt: null },
  { id: 's2', name: 'Last Friday', status: 'Closed', openedAt: '2026-05-23T18:00:00Z', closedAt: '2026-05-23T21:00:00Z' },
];

function apiMock(overrides: Partial<PegboardApi> = {}): PegboardApi {
  return {
    listSessions: vi.fn(() => of(sessions)),
    openSession: vi.fn(() => of({ id: 'new-1' })),
    ...overrides,
  } as unknown as PegboardApi;
}

function create(api: PegboardApi) {
  TestBed.configureTestingModule({
    providers: [
      { provide: PegboardApi, useValue: api },
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

  it('renders sessions from the mocked listSessions', () => {
    const api = apiMock();
    const fixture = create(api);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(api.listSessions).toHaveBeenCalledWith('club-1');
    expect(text).toContain('Tuesday Club Night');
    expect(text).toContain('Last Friday');
    expect(text).toContain('Open');
    expect(text).toContain('Closed');
  });

  it('Open button calls openSession with the entered name', () => {
    const api = apiMock();
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['form'].setValue({ name: 'Wednesday Night' });
    c['onOpen']();

    expect(api.openSession).toHaveBeenCalledWith('club-1', 'Wednesday Night');
  });

  it('on 409 shows a notice and refreshes the list', () => {
    const api = apiMock({ openSession: vi.fn(() => { throw { status: 409 }; }) } as unknown as Partial<PegboardApi>);
    // Re-route the thrown error through an observable error.
    (api.openSession as unknown as ReturnType<typeof vi.fn>).mockImplementation(() => ({
      subscribe: (o: { error: (e: unknown) => void }) => o.error({ status: 409 }),
    }));
    const fixture = create(api);
    const c = fixture.componentInstance as unknown as Record<string, any>;

    c['form'].setValue({ name: 'Dup' });
    c['onOpen']();

    expect(c['notice']()).toBeTruthy();
    // listSessions called once on init + once on refresh after 409.
    expect((api.listSessions as unknown as ReturnType<typeof vi.fn>).mock.calls.length).toBe(2);
  });
});
