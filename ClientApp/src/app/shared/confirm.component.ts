import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ModalComponent } from './modal.component';

// A confirm dialog for destructive actions. Driven by a [message] (null = closed); emits
// (confirmed) or (cancelled). Pages typically hold a pending { message, action } signal
// and run the action on confirm — keeping the closure in TS, not the template.
@Component({
  selector: 'app-confirm',
  imports: [ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-modal [open]="message() !== null" title="Are you sure?" (closed)="cancelled.emit()">
      <p class="font-mono text-sm text-slate-700 dark:text-slate-300">{{ message() }}</p>
      <div class="mt-4 flex justify-end gap-2">
        <button
          type="button"
          (click)="cancelled.emit()"
          class="rounded-md border border-slate-300 px-4 py-2 font-mono text-sm text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
        >
          Cancel
        </button>
        <button
          type="button"
          (click)="confirmed.emit()"
          class="rounded-md bg-red-600 px-4 py-2 font-mono text-sm font-medium text-white hover:bg-red-700"
        >
          Confirm
        </button>
      </div>
    </app-modal>
  `,
})
export class ConfirmComponent {
  readonly message = input<string | null>(null);
  readonly confirmed = output<void>();
  readonly cancelled = output<void>();
}
