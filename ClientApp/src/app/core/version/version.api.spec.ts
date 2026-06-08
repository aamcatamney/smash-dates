import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { VersionApi, VersionInfo } from './version.api';

describe('VersionApi', () => {
  let api: VersionApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(VersionApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('GETs /api/version and returns the version info', () => {
    const info: VersionInfo = { version: 'v2026.6.0' };
    let result: VersionInfo | undefined;
    api.get().subscribe((v) => (result = v));

    const req = httpMock.expectOne('/api/version');
    expect(req.request.method).toBe('GET');
    req.flush(info);
    expect(result).toEqual(info);
  });
});
