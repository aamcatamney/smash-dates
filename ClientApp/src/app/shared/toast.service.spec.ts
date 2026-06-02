import { describe, beforeEach, afterEach, expect, it, vi } from 'vitest';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('pushes success and error toasts with distinct kinds', () => {
    const svc = new ToastService();
    svc.success('Created');
    svc.error('Nope');
    expect(svc.toasts().map((t) => t.kind)).toEqual(['success', 'error']);
    expect(svc.toasts()[0].text).toBe('Created');
  });

  it('auto-dismisses after the timeout', () => {
    const svc = new ToastService();
    svc.success('Created');
    expect(svc.toasts()).toHaveLength(1);
    vi.advanceTimersByTime(4000);
    expect(svc.toasts()).toHaveLength(0);
  });

  it('dismisses a specific toast by id without touching others', () => {
    const svc = new ToastService();
    svc.success('one');
    svc.success('two');
    const firstId = svc.toasts()[0].id;
    svc.dismiss(firstId);
    expect(svc.toasts().map((t) => t.text)).toEqual(['two']);
  });
});
