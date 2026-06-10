import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'warning' | 'error';

export interface Toast {
  readonly id: number;
  readonly text: string;
  readonly kind: ToastKind;
}

// App-wide transient feedback. Pages call success()/warning()/error() after a mutation; the
// ToastContainer (mounted once at the app root) renders them. Success and warning auto-dismiss;
// errors stay until the user dismisses them, so a missed failure doesn't vanish.
// Signal-based so consumers and the container stay zoneless-friendly and OnPush.
@Injectable({ providedIn: 'root' })
export class ToastService {
  private static readonly DISMISS_MS = 4000;
  private nextId = 0;

  private readonly _toasts = signal<readonly Toast[]>([]);
  readonly toasts = this._toasts.asReadonly();

  success(text: string): void {
    this.push(text, 'success');
  }

  warning(text: string): void {
    this.push(text, 'warning');
  }

  error(text: string): void {
    this.push(text, 'error');
  }

  dismiss(id: number): void {
    this._toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private push(text: string, kind: ToastKind): void {
    const id = this.nextId++;
    this._toasts.update((list) => [...list, { id, text, kind }]);
    // Errors persist until manually dismissed; everything else auto-dismisses.
    if (kind !== 'error') {
      setTimeout(() => this.dismiss(id), ToastService.DISMISS_MS);
    }
  }
}
