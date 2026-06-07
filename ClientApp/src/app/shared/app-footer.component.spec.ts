import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { AppFooterComponent } from './app-footer.component';

function create() {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  const fixture = TestBed.createComponent(AppFooterComponent);
  fixture.detectChanges();
  return { fixture, httpMock: TestBed.inject(HttpTestingController) };
}

describe('AppFooterComponent', () => {
  let httpMock: HttpTestingController;

  beforeEach(() => TestBed.resetTestingModule());
  afterEach(() => httpMock.verify());

  it('renders the version from /api/version once it resolves', () => {
    const created = create();
    httpMock = created.httpMock;

    // Nothing rendered until the version arrives.
    expect(created.fixture.nativeElement.textContent).not.toContain('v2026.6.0');

    httpMock.expectOne('/api/version').flush({ version: 'v2026.6.0' });
    created.fixture.detectChanges();

    expect(created.fixture.nativeElement.textContent).toContain('v2026.6.0');
    expect(created.fixture.nativeElement.querySelector('footer')).toBeTruthy();
  });

  it('stays silent (no footer) when the version has not resolved', () => {
    const created = create();
    httpMock = created.httpMock;

    expect(created.fixture.nativeElement.querySelector('footer')).toBeNull();

    httpMock.expectOne('/api/version').flush({ version: 'v2026.6.0' });
  });
});
