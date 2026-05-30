import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';
import { describe, beforeEach, afterEach, expect, it, vi } from 'vitest';
import { ThemeService } from './theme.service';

function stubSystemPrefersDark(prefersDark: boolean): void {
  (window as unknown as { matchMedia: unknown }).matchMedia = vi.fn().mockReturnValue({
    matches: prefersDark,
    media: '(prefers-color-scheme: dark)',
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  });
}

function make(): ThemeService {
  return TestBed.inject(ThemeService);
}

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
    delete (window as unknown as { matchMedia?: unknown }).matchMedia;
    TestBed.configureTestingModule({ providers: [{ provide: DOCUMENT, useValue: document }] });
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
  });

  it('defaults to light when nothing is stored and the system has no dark preference', () => {
    const svc = make();
    expect(svc.theme()).toBe('light');
    expect(svc.isDark()).toBe(false);
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('follows the system preference when nothing is stored', () => {
    stubSystemPrefersDark(true);
    const svc = make();
    expect(svc.theme()).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });

  it('uses an explicit stored choice over the system preference', () => {
    stubSystemPrefersDark(true);
    localStorage.setItem('theme', 'light');
    const svc = make();
    expect(svc.theme()).toBe('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('toggle() flips the theme, persists it, and updates the root class', () => {
    const svc = make();
    expect(svc.theme()).toBe('light');

    svc.toggle();
    expect(svc.theme()).toBe('dark');
    expect(localStorage.getItem('theme')).toBe('dark');
    expect(document.documentElement.classList.contains('dark')).toBe(true);

    svc.toggle();
    expect(svc.theme()).toBe('light');
    expect(localStorage.getItem('theme')).toBe('light');
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });
});
