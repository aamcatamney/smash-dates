# 5. Venue courts vs. simultaneous-match capacity

Date: 2026-06-03

## Status

Accepted

## Context

A [Venue](../../CONTEXT.md#venue) originally had a single `capacity` field, constrained to `1` or `2`, that the scheduler read as "how many [Matches](../../CONTEXT.md#match) may share this `(Venue, Date)` slot". That conflated two different real-world things:

- **Courts** — the number of physical badminton courts a hall has (a club hall might have 2, 4, 6…).
- **Simultaneous matches** — how many league Matches can run at once. A Match is a tie whose rubbers (singles + doubles) are played *in parallel across several courts*, so one Match occupies more than one court. A hall therefore needs *enough courts* before it can run a second Match concurrently.

With the old model there was no concept of a court at all — "capacity" silently meant matches, capped at 2, and a hall with 4 courts couldn't be told apart from a hall with 2. Clubs that wanted to run two Matches a night had no way to express "we have 4 courts", and the number was capped at 2 for everyone regardless of hall size.

## Decision

Split the conflated field and derive the slot capacity.

- A **Venue** stores **`Courts`** (physical court count, ≥ 1) and **`MaxConcurrentMatches`** (the venue's own ceiling, `1` or `2`).
- A **League** gains a **`CourtsPerMatch`** scheduling rule (how many courts one Match occupies), defaulting to `2`.
- The Matches a Venue can host at once in a slot is **computed, never stored**:

  ```
  min( MaxConcurrentMatches , ⌊ Courts ÷ CourtsPerMatch ⌋ )
  ```

The scheduler's hard constraint is unchanged in spirit — "a slot holds no more than its capacity" — but capacity is now this derived value, computed once per generation run from the league's `CourtsPerMatch` (`VenueSlotCapacity.Compute`, a pure, unit-tested function). The scheduler core still receives a single per-venue capacity number, so its placement logic didn't change.

The existing `capacity` column was renamed to `max_concurrent_matches` (it already meant "simultaneous matches") and `courts` was backfilled to `max_concurrent_matches × 2`, so under the default `CourtsPerMatch = 2` every existing venue keeps exactly the slot capacity it had.

## Consequences

**Positive**

- The model now matches reality: a hall's size (`Courts`) and its willingness to run concurrent Matches (`MaxConcurrentMatches`) are separate, and the league decides how court-hungry a Match is (`CourtsPerMatch`).
- Slot capacity is derived in one pure function, so the rule is testable in isolation and the scheduler core stays unaware of courts.
- Backfill preserves every existing schedule's behaviour; no league is silently re-capacitated.

**Negative / limits**

- `MaxConcurrentMatches` is still capped at `1` or `2` by deliberate product choice, not by the courts maths — a 6-court hall won't run 3 league Matches at once even though the courts would allow it. Lifting that cap is a one-line change if a league ever needs it.
- `CourtsPerMatch` is league-wide, not per-division, so a league that mixes formats needing different court counts per Match can't express that yet. Deferred until a real case appears.
