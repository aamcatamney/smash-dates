import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { ModalComponent } from './modal.component';
import { ImportResult } from './import-result';

// Reusable CSV bulk-import dialog. The parent supplies the expected [columns] and wires
// [result]/[busy], reacting to (submit) with the file's text. The component handles file
// reading, a downloadable template, and rendering the per-row result/errors.
@Component({
  selector: 'app-csv-import',
  imports: [ModalComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-modal [open]="open()" [title]="title()" (closed)="onClose()">
      <div class="grid gap-3 font-mono text-sm">
        <p class="text-slate-600 dark:text-slate-400">
          CSV columns:
          <span class="font-semibold text-slate-900 dark:text-slate-100">{{ columns().join(', ') }}</span>
        </p>

        <button
          type="button"
          (click)="downloadTemplate()"
          class="justify-self-start rounded-md border border-slate-300 px-3 py-1 text-xs text-slate-700 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"
        >
          ⬇ Download template
        </button>

        <label class="grid gap-1">
          <span class="text-xs uppercase tracking-wider text-slate-600 dark:text-slate-400">CSV file</span>
          <input
            type="file"
            accept=".csv,text/csv"
            (change)="onFile($event)"
            class="rounded-md border border-slate-300 px-3 py-2 text-sm file:mr-3 file:rounded file:border-0 file:bg-slate-100 file:px-3 file:py-1 file:text-slate-700 focus:outline-none focus:ring-2 focus:ring-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:file:bg-slate-700 dark:file:text-slate-200 dark:focus:ring-slate-100"
          />
        </label>

        <button
          type="button"
          [disabled]="!csv() || busy()"
          (click)="onImport()"
          class="justify-self-start rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-amber-300 hover:bg-slate-800 disabled:opacity-50 dark:bg-amber-400 dark:text-slate-900 dark:hover:bg-amber-300"
        >
          {{ busy() ? 'Importing…' : 'Import' }}
        </button>

        @if (result(); as r) {
          <div class="rounded-md border border-slate-200 bg-slate-50 px-3 py-2 dark:border-slate-800 dark:bg-slate-950">
            <p class="text-slate-700 dark:text-slate-300">
              Created <span class="font-semibold">{{ r.created }}</span> ·
              Updated <span class="font-semibold">{{ r.updated }}</span> ·
              Errors <span class="font-semibold">{{ r.errors.length }}</span>
            </p>
            @if (r.errors.length) {
              <ul class="mt-2 grid gap-1">
                @for (e of r.errors; track $index) {
                  <li class="text-xs text-red-600 dark:text-red-400">Row {{ e.row }}: {{ e.message }}</li>
                }
              </ul>
            }
          </div>
        }

        @if (error()) {
          <p class="text-sm text-red-600 dark:text-red-400" role="alert">{{ error() }}</p>
        }
      </div>
    </app-modal>
  `,
})
export class CsvImportComponent {
  private readonly doc = inject(DOCUMENT);

  readonly open = input(false);
  readonly title = input('Import CSV');
  readonly columns = input<string[]>([]);
  readonly sample = input<string | null>(null);
  readonly result = input<ImportResult | null>(null);
  readonly busy = input(false);

  readonly closed = output<void>();
  readonly submit = output<string>();

  protected readonly csv = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);

  protected onFile(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0];
    this.error.set(null);
    this.csv.set(null);
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => this.csv.set(String(reader.result ?? ''));
    reader.onerror = () => this.error.set('Could not read the file.');
    reader.readAsText(file);
  }

  protected onImport(): void {
    const text = this.csv();
    if (text !== null) this.submit.emit(text);
  }

  protected onClose(): void {
    this.csv.set(null);
    this.error.set(null);
    this.closed.emit();
  }

  protected downloadTemplate(): void {
    const header = this.columns().join(',');
    const sample = this.sample();
    const content = header + (sample ? '\n' + sample : '') + '\n';
    const blob = new Blob([content], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const anchor = this.doc.createElement('a');
    anchor.href = url;
    anchor.download = 'template.csv';
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
