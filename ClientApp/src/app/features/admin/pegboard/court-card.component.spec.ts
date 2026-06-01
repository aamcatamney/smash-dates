import { TestBed } from '@angular/core/testing';
import { describe, beforeEach, expect, it } from 'vitest';
import { CourtCardComponent } from './court-card.component';
import { BoardCourt } from '../pegboard.api';

const freeCourt: BoardCourt = { id: 'c1', label: 'Court 1', activeGame: null };
const busyCourt: BoardCourt = {
  id: 'c2',
  label: 'Court 2',
  activeGame: {
    id: 'g1',
    type: 'Doubles',
    players: [
      { attendanceId: 'a1', displayName: 'Alice', gender: 'Female', grade: null, side: 'A' },
      { attendanceId: 'a2', displayName: 'Bob', gender: 'Male', grade: null, side: 'B' },
    ],
  },
};

function render(court: BoardCourt, live: boolean) {
  const fixture = TestBed.createComponent(CourtCardComponent);
  fixture.componentRef.setInput('court', court);
  fixture.componentRef.setInput('live', live);
  fixture.detectChanges();
  return fixture;
}

describe('CourtCardComponent', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('splits the active game players by side', () => {
    const text = (render(busyCourt, true).nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Side A');
    expect(text).toContain('Alice');
    expect(text).toContain('Bob');
    expect(text).toContain('Doubles');
  });

  it('shows Fill on a free court when live', () => {
    const text = (render(freeCourt, true).nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Fill court');
  });

  it('hides all controls when not live', () => {
    const free = (render(freeCourt, false).nativeElement as HTMLElement).textContent ?? '';
    expect(free).not.toContain('Fill court');
    const busy = (render(busyCourt, false).nativeElement as HTMLElement).textContent ?? '';
    expect(busy).not.toContain('Finish');
    expect(busy).not.toContain('Cancel');
    // The game itself still renders read-only.
    expect(busy).toContain('Alice');
  });

  it('emits finish when Finish is clicked', () => {
    const fixture = render(busyCourt, true);
    let fired = false;
    fixture.componentInstance.finish.subscribe(() => (fired = true));
    const btn = (fixture.nativeElement as HTMLElement).querySelector('button');
    // First button on a busy court is Finish.
    btn?.dispatchEvent(new Event('click'));
    expect(fired).toBe(true);
  });
});
