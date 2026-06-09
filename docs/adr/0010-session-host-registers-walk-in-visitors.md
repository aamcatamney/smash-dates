# 10. A session host may register walk-in visitors as players

Date: 2026-06-09

## Status

Accepted

## Context

On a club night the common case is adding people who are **already on the club's roster**, so the
pegboard "Add player" flow now defaults to picking an existing [Player](../../CONTEXT.md#player).
The occasional case is a **walk-in visitor** who isn't on the roster yet. Per the product decision,
a walk-in should become a **real Player with a `Visitor` affiliation** (not an ephemeral guest), so
they are remembered and selectable next time — and so their per-night stats attach to a record.

That creates an authority tension. Creating Player records and club affiliations is otherwise a
**ClubAdmin-only** operation (`POST /api/clubs/{id}/players`). But a [Pegboard Session](../../CONTEXT.md#pegboard-session)
can be run by a **SessionHost** who is *not* a club admin (see [Roles](../../CONTEXT.md#roles-and-access)).
If visitor-creation required ClubAdmin, a non-admin host couldn't add a walk-in mid-session — the
exact moment the feature is for.

## Decision

The pegboard add-attendance endpoint (`POST .../attendances`) accepts a `NewVisitor` block and, when
present, **creates the Player + `Visitor` link itself**, then adds them to the board — authorized as
a **session runner** (`SessionHost@Club`, `ClubAdmin@Club` or `SystemAdmin`), the same authority as
every other session mutation. It deliberately does **not** require ClubAdmin.

This is a scoped widening: a session host can create `Visitor` players **on the club whose session
they are running**, and only through the running session. They gain no other roster-write powers
(rename, delete, change affiliation type, register for leagues remain ClubAdmin-only).

## Considered options

- **Require ClubAdmin to add a walk-in (status quo for player creation).** Cleanest authority model,
  but breaks the night-of flow whenever the host isn't also an admin. Rejected — defeats the feature.
- **Add walk-ins as ephemeral guests, not real players (the prior behaviour).** No new authority
  needed, but the person isn't remembered and stats don't attach — contrary to the product call.
  Rejected (the ephemeral-guest path still exists in the model/back-end, just not surfaced).
- **Let a session host create visitors, scoped to the live session (chosen).** Matches who is
  actually present and responsible on the night; keeps the widening narrow and auditable.

## Consequences

**Positive**
- The realistic club-night flow works for session hosts, not just admins.
- Walk-ins become durable roster records (Visitors), selectable next time, with stats attached.

**Negative**
- A SessionHost can now create Player + `Visitor` rows on their club — a deliberate exception to the
  "only ClubAdmin writes the roster" rule. This ADR records it as intentional, not an oversight.
- Combined with [ADR-0007](0007-no-club-level-player-dedup.md) (no by-name dedup), a careless host
  can create duplicate visitor records; reconciliation is the future identity-merge feature's job.

Relates to [ADR-0007](0007-no-club-level-player-dedup.md) (players enter the system per-club, no
dedup) and [ADR-0009](0009-scheduled-pegboard-sessions.md) (the broader club-night work).
