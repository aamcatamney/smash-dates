import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'theme';

// Owns the light/dark theme. On first load it follows the OS preference; once the user
// toggles, the explicit choice is persisted to localStorage and wins thereafter. The
// active theme is reflected as a `dark` class on <html>, which Tailwind's dark: variants
// key off (see the @custom-variant in styles.css). A matching inline script in index.html
// applies the same class before first paint to avoid a flash of the wrong theme.
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly _theme = signal<Theme>('light');

  readonly theme = this._theme.asReadonly();
  readonly isDark = computed(() => this._theme() === 'dark');

  constructor() {
    this.set(this.initial());
  }

  toggle(): void {
    this.set(this._theme() === 'dark' ? 'light' : 'dark');
  }

  set(theme: Theme): void {
    this._theme.set(theme);
    this.persist(theme);
    this.apply(theme);
  }

  private initial(): Theme {
    const stored = this.read();
    if (stored === 'light' || stored === 'dark') return stored;
    return this.systemPrefersDark() ? 'dark' : 'light';
  }

  private apply(theme: Theme): void {
    this.doc.documentElement.classList.toggle('dark', theme === 'dark');
  }

  private systemPrefersDark(): boolean {
    const win = this.doc.defaultView;
    return win?.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
  }

  private read(): string | null {
    try {
      return this.doc.defaultView?.localStorage.getItem(STORAGE_KEY) ?? null;
    } catch {
      return null;
    }
  }

  private persist(theme: Theme): void {
    try {
      this.doc.defaultView?.localStorage.setItem(STORAGE_KEY, theme);
    } catch {
      // Private-mode / blocked storage: theme still applies for this session.
    }
  }
}
