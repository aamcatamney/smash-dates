# 8. Expose a public, anonymous, read-only view

Date: 2026-06-09

## Status

Accepted

(The issue that prompted this asked for "ADR 0005"; that number was already taken by
[0005-venue-courts-vs-match-capacity](0005-venue-courts-vs-match-capacity.md), so this decision is
recorded here.)

## Context

smash-dates was built authed-only: every page sat behind the cookie auth + `authGuard`, and an
anonymous visitor hitting any route — including the app root `/` — was bounced to the sign-in page.
`CONTEXT.md` stated outright that "there is no anonymous public view".

That posture costs reach. League standings and fixtures are inherently spectator-facing: players,
parents and club members want to glance at the table or the next fixture without an account. A
sign-in wall on read-only, non-sensitive information is friction with no security benefit.

The tension is data exposure. The app holds plenty that must **not** leak to the public: club
contact emails, notes, membership state, player rosters, blocked dates, the pegboard, venues, and
every admin action.

## Decision

Expose a public, anonymous, **read-only** view, served login-free under `/public`, reversing the
earlier authed-only stance.

- **Front door.** The app root `/` branches by auth: authenticated users get their dashboard;
  anonymous visitors are sent to the public landing (`landingGuard` → `/public`) rather than to
  sign-in. Login / Register stay reachable from the public header.
- **Safe public projection.** An anonymous visitor may read only: League names, division
  **standings**, and **fixtures** (date, home/away team, status) for Seasons that have a schedule
  (`Proposed` / `Active` / `Closed`). Everything else stays behind auth. Specifically hidden:
  Club contact details, notes, membership state, player rosters, blocked dates, the pegboard,
  **venues** (fixtures show no venue to anonymous viewers), and all admin/mutating actions.
- The public endpoints live under their own `/api/public/*` group with no auth, returning DTOs
  shaped to the projection above — not the authenticated DTOs trimmed at the edge.

## Consequences

- Spectator reach without an account; the read surface is now a deliberate, reviewed projection
  rather than an implicit "whatever the authed API returns".
- Any new public-facing field is a conscious addition to the projection — the default remains
  hidden. New publicly-readable data must be added to the `/api/public/*` DTOs explicitly.
- `CONTEXT.md` is updated: the "no anonymous public view" note is replaced by the **Anonymous
  public view** definition and its PII-free projection.
