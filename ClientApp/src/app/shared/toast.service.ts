import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error';

export interface Toast {
  readonly id: number;
  readonly text: string;
  readonly kind: ToastKind;
}

// App-wide transient feedback. Pages call success()/error() after a mutation; the
// ToastContainer (mounted once at the app root) renders them and they auto-dismiss.
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

  error(text: string): void {
    this.push(text, 'error');
  }

  dismiss(id: number): void {
    this._toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private push(text: string, kind: ToastKind): void {
    const id = this.nextId++;
    this._toasts.update((list) => [...list, { id, text, kind }]);
    setTimeout(() => this.dismiss(id), ToastService.DISMISS_MS);
  }
}
