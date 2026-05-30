import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  input,
  output,
  viewChild,
} from '@angular/core';

// Reusable modal built on the native <dialog> element: free modality, Esc-to-close,
// backdrop and focus handling. Drive it with [open] and react to (closed); project the
// dialog body (typically a form) as content.
@Component({
  selector: 'app-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      #dlg
      (close)="closed.emit()"
      (click)="onBackdropClick($event)"
      class="m-auto w-full max-w-lg rounded-md border border-slate-300 bg-white p-0 shadow-2xl dark:border-slate-700 dark:bg-slate-900"
    >
      <div class="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
        <h2 class="font-mono text-sm font-semibold uppercase tracking-wider text-slate-900 dark:text-slate-100">{{ title() }}</h2>
        <button
          type="button"
          aria-label="Close"
          (click)="dlg.close()"
          class="rounded px-2 py-0.5 font-mono text-slate-500 hover:bg-slate-100 dark:text-slate-400 dark:hover:bg-slate-800"
        >
          ✕
        </button>
      </div>
      <div class="p-4">
        <ng-content />
      </div>
    </dialog>
  `,
  styles: [
    `
      dialog::backdrop {
        background: rgb(15 23 42 / 0.5);
      }
    `,
  ],
})
export class ModalComponent {
  readonly title = input('');
  readonly open = input(false);
  readonly closed = output<void>();

  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dlg');

  constructor() {
    effect(() => {
      const el = this.dialog().nativeElement;
      if (this.open() && !el.open) el.showModal();
      else if (!this.open() && el.open) el.close();
    });
  }

  protected onBackdropClick(event: MouseEvent): void {
    // A click whose target is the <dialog> itself is a click on the backdrop.
    if (event.target === this.dialog().nativeElement) this.dialog().nativeElement.close();
  }
}
