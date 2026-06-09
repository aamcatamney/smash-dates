# 9. Scheduled pegboard sessions open manually

Date: 2026-06-09

## Status

Accepted

## Context

A [Pegboard Session](../../CONTEXT.md#pegboard-session) was a wholly on-demand thing: a host hit
"Open session" and a live board existed *now*, with status `Open → Closed` and a database rule of
**at most one `Open` session per club**. Clubs wanted to plan ahead — "create next week's session"
— recording when and where it will run (an optional start time, duration and [Venue](../../CONTEXT.md#venue))
before the night arrives, so the board is ready and members can see what's coming.

The open design question was the lifecycle: how does a planned session become the live board?

## Decision

Add a leading state, `Scheduled`, before `Open`: lifecycle is now `Scheduled → Open → Closed`.

- A `Scheduled` session carries a required **ScheduledDate** and optional **StartTime**,
  **DurationMinutes** and **Venue** (a nullable reference to one of the club's existing Venue
  records). These are **informational** — they describe when and where, and do not drive behaviour.
- **A host opens a scheduled session manually** (`Scheduled → Open`) when the night begins. There
  is no background job that auto-opens at the start time, and duration does not auto-close.
- A club may have **any number** of `Scheduled` sessions at once; the existing one-`Open`-per-club
  unique index is unchanged, so opening one is rejected (409) while another is already live.
- A `Scheduled` session may be **edited or deleted** while still scheduled. Once `Open` it follows
  the existing lifecycle (only `Closed`, never deleted — closed sessions are retained as history).
- Selecting a Venue records the location only; it does **not** seed pegboard Courts from the
  Venue's court count. Courts stay host-managed on the night, as before.

Scheduling, opening, editing and deleting all require the same authority as running a session:
`SessionHost@Club`, `ClubAdmin@Club` or `SystemAdmin`.

## Considered options

- **Auto-open at the start time (and auto-close after the duration).** A scheduler flips the
  session live automatically. Rejected: it adds a background job and timezone/edge-case surface,
  and — worse — a live board could appear with nobody present to run it. The board is an
  attended, in-person tool; "live" should mean a host is there.
- **One upcoming session at a time.** Simpler invariant, but it can't express "the next few
  weeks", which is the actual request. Rejected.
- **A free-text location instead of a Venue link.** Avoids coupling to the league Venue model, but
  throws away the structured Venue data the club already maintains and offers no reuse. Rejected in
  favour of a nullable reference to an existing Venue (validated to belong to the club).

## Consequences

**Positive**
- Clubs can plan ahead; members see upcoming nights with their time and place.
- The one-`Open`-per-club invariant and the attended-board assumption are both preserved — a
  scheduled session is inert until a human opens it.
- Reuses existing Venue records rather than inventing a parallel location concept.

**Negative**
- `opened_at` is now nullable (a `Scheduled` row has no open time yet), and the status check and a
  new lifecycle check constraint had to change — see migration `0028_schedule_pegboard_sessions.sql`.
- "Scheduled" sessions can pile up if never opened or cleaned; they are dropped manually, not
  expired automatically.

Builds on [ADR-0004](0004-sse-for-pegboard-live-updates.md) (the live board) and treats Venues
per [ADR-0005](0005-venue-courts-vs-match-capacity.md) as a club's halls, here used only as an
optional location label for a club night.
