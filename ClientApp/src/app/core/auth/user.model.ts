export interface AuthenticatedUser {
  id: string;
  email: string;
  displayName: string | null;
  isSystemAdmin: boolean;
}

export interface RoleGrant {
  id: string;
  name: string;
}

export interface MyGrants {
  systemAdmin: boolean;
  leagueAdmin: RoleGrant[];
  clubAdmin: RoleGrant[];
  sessionHost: RoleGrant[];
}

export type AuthStatus = 'unknown' | 'anonymous' | 'authed';

export interface ProblemDetails {
  type?: string;
  title?: string;
  detail?: string;
  status?: number;
}
