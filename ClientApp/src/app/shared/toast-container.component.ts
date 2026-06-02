import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

// Renders the active toasts in a fixed, screen-reader-announced region. Mounted once at the
// app root. Success/error are distinguished by an icon + colour (not colour alone).
@Component({
  selector: 'app-toast-container',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      aria-live="polite"
      aria-atomic="false"
      class="pointer-events-none fixed inset-x-0 bottom-4 z-50 flex flex-col items-center gap-2 px-4"
    >
      @for (t of toasts.toasts(); track t.id) {
        <div
          role="status"
          class="pointer-events-auto flex w-full max-w-md items-start gap-3 rounded-md border-l-4 px-4 py-3 font-mono text-sm shadow-lg"
          [class.border-emerald-500]="t.kind === 'success'"
          [class.bg-emerald-50]="t.kind === 'success'"
          [class.text-emerald-900]="t.kind === 'success'"
          [class.dark:border-emerald-400]="t.kind === 'success'"
          [class.dark:bg-emerald-950]="t.kind === 'success'"
          [class.dark:text-emerald-200]="t.kind === 'success'"
          [class.border-red-500]="t.kind === 'error'"
          [class.bg-red-50]="t.kind === 'error'"
          [class.text-red-900]="t.kind === 'error'"
          [class.dark:border-red-400]="t.kind === 'error'"
          [class.dark:bg-red-950]="t.kind === 'error'"
          [class.dark:text-red-200]="t.kind === 'error'"
        >
          <span aria-hidden="true" class="font-semibold">{{
            t.kind === 'success' ? '✓' : '!'
          }}</span>
          <span class="flex-1">{{ t.text }}</span>
          <button
            type="button"
            aria-label="Dismiss"
            (click)="toasts.dismiss(t.id)"
            class="-my-1 -mr-1 rounded px-2 py-1 text-current opacity-70 hover:opacity-100"
          >
            ✕
          </button>
        </div>
      }
    </div>
  `,
})
export class ToastContainerComponent {
  protected readonly toasts = inject(ToastService);
}
