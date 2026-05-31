import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';

// A small "subscribe to fixtures (iCal)" control. Given the mint endpoint, it fetches the
// tokenised feed URL on demand and reveals it with copy + webcal links.
@Component({
  selector: 'app-calendar-subscribe',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      (click)="toggle()"
      class="rounded-md border border-slate-300 px-3 py-1 font-mono text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
    >
      📅 {{ label() }}
    </button>
    @if (open()) {
      <div class="mt-2 grid gap-2 rounded-md border border-slate-200 bg-slate-50 p-3 font-mono text-xs dark:border-slate-800 dark:bg-slate-950">
        @if (url(); as u) {
          <p class="text-slate-600 dark:text-slate-400">Subscribe in your calendar app, or copy the link:</p>
          <div class="flex items-center gap-2">
            <input
              type="text"
              readonly
              [value]="u"
              aria-label="Calendar feed URL"
              (focus)="selectAll($event)"
              class="flex-1 rounded border border-slate-300 px-2 py-1 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
            />
            <button type="button" (click)="copy(u)" class="rounded border border-slate-300 px-2 py-1 dark:border-slate-700 dark:text-slate-300">{{ copied() ? 'Copied' : 'Copy' }}</button>
            <a [href]="webcal()" class="rounded border border-slate-300 px-2 py-1 text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800">Add to calendar</a>
          </div>
        } @else if (error()) {
          <p class="text-red-600 dark:text-red-400" role="alert">{{ error() }}</p>
        } @else {
          <p class="text-slate-500 dark:text-slate-400">Loading…</p>
        }
      </div>
    }
  `,
})
export class CalendarSubscribeComponent {
  private readonly http = inject(HttpClient);
  private readonly doc = inject(DOCUMENT);

  readonly endpoint = input.required<string>();
  readonly label = input('Subscribe (iCal)');

  protected readonly open = signal(false);
  protected readonly url = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly copied = signal(false);

  protected readonly webcal = computed(() => (this.url() ?? '').replace(/^https?:/, 'webcal:'));

  protected toggle(): void {
    const next = !this.open();
    this.open.set(next);
    if (next && this.url() === null && this.error() === null) this.load();
  }

  private load(): void {
    this.http.get<{ url: string }>(this.endpoint()).subscribe({
      next: (r) => this.url.set((this.doc.defaultView?.location.origin ?? '') + r.url),
      error: () => this.error.set('Could not load the calendar link.'),
    });
  }

  protected copy(value: string): void {
    this.doc.defaultView?.navigator.clipboard?.writeText(value).then(() => {
      this.copied.set(true);
      this.doc.defaultView?.setTimeout(() => this.copied.set(false), 2000);
    });
  }

  protected selectAll(event: Event): void {
    (event.target as HTMLInputElement).select();
  }
}
