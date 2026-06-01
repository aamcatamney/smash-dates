import { TestBed } from '@angular/core/testing';
import { describe, beforeEach, expect, it } from 'vitest';
import { FillDialogComponent, StartGamePayload } from './fill-dialog.component';
import { BoardAttendee } from '../pegboard.api';

const waiting: BoardAttendee[] = [
  {
    id: 'a1',
    playerId: null,
    displayName: 'Alice',
    gender: 'Female',
    grade: null,
    status: 'Waiting',
    waitingSince: '2026-05-31T10:00:00Z',
    gamesPlayed: 0,
    gamesWon: 0,
  },
  {
    id: 'a2',
    playerId: null,
    displayName: 'Bob',
    gender: 'Male',
    grade: null,
    status: 'Waiting',
    waitingSince: '2026-05-31T10:01:00Z',
    gamesPlayed: 0,
    gamesWon: 0,
  },
];

// Rendered closed: jsdom's <dialog> has no showModal(), and the assignment logic under test
// doesn't depend on the dialog being open.
function render(open = false) {
  const fixture = TestBed.createComponent(FillDialogComponent);
  fixture.componentRef.setInput('open', open);
  fixture.componentRef.setInput('waiting', waiting);
  fixture.componentRef.setInput('courtLabel', 'Court 1');
  fixture.detectChanges();
  return fixture;
}

describe('FillDialogComponent', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('cycles a player through A -> B -> unassigned', () => {
    const fixture = render();
    const c = fixture.componentInstance as unknown as Record<string, any>;
    expect(c['sideOf']('a1')).toBeNull();
    c['cycleSide']('a1');
    expect(c['sideOf']('a1')).toBe('A');
    c['cycleSide']('a1');
    expect(c['sideOf']('a1')).toBe('B');
    c['cycleSide']('a1');
    expect(c['sideOf']('a1')).toBeNull();
  });

  it('can only start with at least one player on each side', () => {
    const fixture = render();
    const c = fixture.componentInstance as unknown as Record<string, any>;
    c['cycleSide']('a1'); // A
    expect(c['canStart']()).toBe(false);
    c['cycleSide']('a2'); // A
    c['cycleSide']('a2'); // B
    expect(c['canStart']()).toBe(true);
  });

  it('emits a start payload with the sides and current type', () => {
    const fixture = render();
    const c = fixture.componentInstance as unknown as Record<string, any>;
    let payload: StartGamePayload | null = null;
    fixture.componentInstance.start.subscribe((p) => (payload = p));

    c['type'].set('Singles');
    c['cycleSide']('a1'); // A
    c['cycleSide']('a2'); // A
    c['cycleSide']('a2'); // B
    c['onStart']();

    expect(payload).toEqual({ type: 'Singles', sideA: ['a1'], sideB: ['a2'] });
  });

  it('emits autoFill with the current type', () => {
    const fixture = render();
    const c = fixture.componentInstance as unknown as Record<string, any>;
    let emitted: string | null = null;
    fixture.componentInstance.autoFill.subscribe((t) => (emitted = t));
    c['type'].set('Mixed');
    fixture.detectChanges();

    const buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'),
    );
    buttons.find((b) => /Auto-fill/.test(b.textContent ?? ''))?.dispatchEvent(new Event('click'));
    expect(emitted!).toBe('Mixed');
  });

  it('seeds the assignment from a pushed suggestion', () => {
    const fixture = render();
    fixture.componentRef.setInput('suggestion', { sideA: ['a1'], sideB: ['a2'] });
    fixture.detectChanges();
    const c = fixture.componentInstance as unknown as Record<string, any>;
    expect(c['sideOf']('a1')).toBe('A');
    expect(c['sideOf']('a2')).toBe('B');
    expect(c['canStart']()).toBe(true);
  });
});
