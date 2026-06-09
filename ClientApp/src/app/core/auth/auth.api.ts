import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthenticatedUser, MyGrants } from './user.model';

export interface LoginPayload {
  email: string;
  password: string;
  rememberMe: boolean;
}

export interface RegisterPayload {
  email: string;
  password: string;
  displayName: string | null;
}

// Register either signs in the bootstrap admin (returns the user) or, for everyone else,
// kicks off email verification and returns a flag instead of a session.
export type RegisterResult = AuthenticatedUser | { emailVerificationRequired: true };

export function isVerificationRequired(
  r: RegisterResult,
): r is { emailVerificationRequired: true } {
  return (r as { emailVerificationRequired?: boolean }).emailVerificationRequired === true;
}

@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/auth';

  me(): Observable<AuthenticatedUser> {
    return this.http.get<AuthenticatedUser>(`${this.base}/me`);
  }

  myGrants(): Observable<MyGrants> {
    return this.http.get<MyGrants>(`${this.base}/me/grants`);
  }

  updateDisplayName(displayName: string | null): Observable<AuthenticatedUser> {
    return this.http.patch<AuthenticatedUser>(`${this.base}/me`, { displayName });
  }

  login(payload: LoginPayload): Observable<AuthenticatedUser> {
    return this.http.post<AuthenticatedUser>(`${this.base}/login`, payload);
  }

  register(payload: RegisterPayload): Observable<RegisterResult> {
    return this.http.post<RegisterResult>(`${this.base}/register`, payload);
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.base}/logout`, null);
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>(`${this.base}/forgot-password`, { email });
  }

  resetPassword(token: string, password: string): Observable<void> {
    return this.http.post<void>(`${this.base}/reset-password`, { token, password });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.base}/change-password`, { currentPassword, newPassword });
  }

  verifyEmail(token: string): Observable<void> {
    return this.http.post<void>(`${this.base}/verify-email`, { token });
  }

  resendVerification(email: string): Observable<void> {
    return this.http.post<void>(`${this.base}/resend-verification`, { email });
  }
}
