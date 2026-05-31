import { TestBed } from '@angular/core/testing';
import { describe, beforeEach, expect, it } from 'vitest';
import { CsvImportComponent } from './csv-import.component';

function create() {
  TestBed.configureTestingModule({});
  const fixture = TestBed.createComponent(CsvImportComponent);
  fixture.componentRef.setInput('columns', ['name', 'gender']);
  return fixture.componentInstance as unknown as Record<string, any>;
}

describe('CsvImportComponent', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('emits the read CSV text on import', () => {
    const c = create();
    let emitted: string | undefined;
    (c['submit'] as { subscribe: (fn: (v: string) => void) => void }).subscribe((v) => (emitted = v));

    c['csv'].set('name,gender\nJane,Female');
    c['onImport']();

    expect(emitted).toBe('name,gender\nJane,Female');
  });

  it('does not emit when no file has been read', () => {
    const c = create();
    let emitted = false;
    (c['submit'] as { subscribe: (fn: (v: string) => void) => void }).subscribe(() => (emitted = true));

    c['onImport']();

    expect(emitted).toBe(false);
  });
});
