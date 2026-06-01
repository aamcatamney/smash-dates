import { TestBed } from '@angular/core/testing';
import { describe, beforeEach, expect, it } from 'vitest';
import { WaitingQueueComponent, waitMinutes } from './waiting-queue.component';
import { BoardAttendee } from '../pegboard.api';

const attendees: BoardAttendee[] = [
  {
    id: 'a1',
    playerId: null,
    displayName: 'Carol',
    gender: 'Female',
    grade: 1,
    status: 'Waiting',
    waitingSince: '2026-05-31T10:00:00Z',
    gamesPlayed: 0,
    gamesWon: 0,
  },
  {
    id: 'a2',
    playerId: null,
    displayName: 'Dan',
    gender: 'Male',
    grade: null,
    status: 'Resting',
    waitingSince: '2026-05-31T09:30:00Z',
    gamesPlayed: 3,
    gamesWon: 2,
  },
  {
    id: 'a3',
    playerId: null,
    displayName: 'Eve',
    gender: 'Female',
    grade: null,
    status: 'Left',
    waitingSince: '2026-05-31T08:00:00Z',
    gamesPlayed: 5,
    gamesWon: 1,
  },
];

function render(live: boolean, now = 0) {
  const fixture = TestBed.createComponent(WaitingQueueComponent);
  fixture.componentRef.setInput('attendees', attendees);
  fixture.componentRef.setInput('live', live);
  fixture.componentRef.setInput('now', now);
  fixture.detectChanges();
  return fixture;
}

describe('waitMinutes', () => {
  it('floors elapsed minutes and never goes negative', () => {
    const since = '2026-05-31T10:00:00Z';
    const now = Date.parse('2026-05-31T10:12:30Z');
    expect(waitMinutes(since, now)).toBe(12);
    expect(waitMinutes(since, Date.parse('2026-05-31T09:00:00Z'))).toBe(0);
  });
});

describe('WaitingQueueComponent', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('live: shows the waiting queue with wait time and a resting section', () => {
    const now = Date.parse('2026-05-31T10:05:00Z');
    const text = (render(true, now).nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Waiting');
    expect(text).toContain('Carol');
    expect(text).toContain('waiting 5m');
    expect(text).toContain('Resting');
    expect(text).toContain('Dan');
    expect(text).toContain('Rest');
  });

  it('closed: shows a read-only roster sorted by games played, no action buttons', () => {
    const text = (render(false).nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Attendees');
    expect(text).toContain('Eve'); // Left but played 5 — still in history roster
    expect(text).toContain('5 played · 1 won');
    expect(text).not.toContain('Rest');
    expect(text).not.toContain('Leave');
  });

  it('emits rest with the attendee when the Rest button is clicked', () => {
    const fixture = render(true);
    let emitted: BoardAttendee | null = null;
    fixture.componentInstance.rest.subscribe((a) => (emitted = a));
    const btn = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[aria-label="Rest Carol"]',
    );
    btn?.dispatchEvent(new Event('click'));
    expect(emitted!).toBe(attendees[0]);
  });
});
