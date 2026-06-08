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
});
