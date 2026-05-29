# 2. Season Weeks ordered by start date, not an explicit ordinal

Date: 2026-05-28

## Status

Accepted

## Context

CONTEXT.md describes a Season as having an "explicit ordered list of Weeks". A literal reading suggests storing an explicit ordering (an `ordinal`/`position` column) that the admin controls independently of the dates.

Each Week, however, already carries `(StartDate, EndDate, WeekType)`, and the domain forbids Weeks from overlapping. That means the calendar dates *already* impose a total, unambiguous order on the Weeks — an ordinal would be a second source of truth for the same fact.

The slice that introduces Seasons manages Weeks as a replace-all set (`PUT /seasons/{id}/weeks`) configured during `Draft`, not as individually reordered rows.

## Decision

Do not store an ordinal/position column on `weeks`. Derive Week order from `start_date` (`ORDER BY start_date`). Enforce non-overlap as a validation rule so the date-derived order stays total and unambiguous.

## Consequences

**Positive**
- One source of truth for ordering; no risk of an ordinal disagreeing with the dates.
- Replace-all Week editing needs no ordinal-renumbering logic.
- Simpler schema and inserts.

**Negative**
- The phrase "explicit ordered list" in the glossary no longer maps to a literal stored order; CONTEXT.md has been amended to say order is derived from `StartDate`.
- If a future requirement ever needs two Weeks to share a start date (it does not today, and non-overlap forbids it), an ordinal would have to be reintroduced via migration.

**Mitigation**
- Non-overlap validation is enforced on create and replace, keeping the date order well-defined.
