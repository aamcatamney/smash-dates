import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { AuthApi } from './auth.api';

describe('AuthApi', () => {
  let api: AuthApi;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(AuthApi);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('changePassword POSTs the current and new passwords', () => {
    api.changePassword('old-password-1234', 'new-password-5678').subscribe();

    const req = httpMock.expectOne('/api/auth/change-password');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      currentPassword: 'old-password-1234',
      newPassword: 'new-password-5678',
    });
    req.flush(null);
  });

  it('myGrants GETs the current user grants', () => {
    api.myGrants().subscribe();

    const req = httpMock.expectOne('/api/auth/me/grants');
    expect(req.request.method).toBe('GET');
    req.flush({ systemAdmin: false, leagueAdmin: [], clubAdmin: [], sessionHost: [] });
  });

  it('updateDisplayName PATCHes the new display name', () => {
    api.updateDisplayName('New Name').subscribe();

    const req = httpMock.expectOne('/api/auth/me');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ displayName: 'New Name' });
    req.flush({ id: 'u1', email: 'u@example.com', displayName: 'New Name', isSystemAdmin: false });
  });
});
