# 7. No club-level player de-duplication; merge identities later

Date: 2026-06-07

## Status

Accepted

## Context

A [Player](../../CONTEXT.md#player) is an admin-managed person record. Across many Leagues and
many Clubs there will be many genuinely different people sharing a name ("John Smith"), so a name
(even name + gender) is **not** a reliable identity key. The earlier club-player UX leaned on that
key anyway: the CSV importer could `useExisting` to reuse a same-name global player, the "Add
player" dialog let an admin search every Club's players by name and link one, and the transfer-in
search enumerated players globally by name. Each of those risked silently fusing two different
people into one record at data-entry time — the hardest kind of mistake to unpick.

## Decision

A Club admin **always creates a new Player record** when adding to their own roster — via the Add
dialog or the CSV importer. There is **no by-name lookup of players across other Clubs** at the
Club level. Duplicate identities are accepted as a normal, transient state.

The only cross-Club player path is a [Registration Transfer](../../CONTEXT.md#registration-transfer),
and its candidate search is **scoped to the Leagues the receiving Club is an Accepted member of**:
it returns Confirmed registrations in those Leagues only, each carrying its `(League, Discipline,
current Club)` so the admin disambiguates real people by context, not by name alone. Reconciling
the duplicate records a person accumulates across Clubs/Leagues is deferred to a **future identity
merge** feature, owned by the person, not by any one Club admin.

## Considered options

- **Match on name + gender (the old `useExisting`/global search).** Cheap, but conflates distinct
  people and the failure is invisible until much later. Rejected — wrong default once the dataset
  has many namesakes.
- **Require a stronger identity key at entry (email / DOB).** Players are not login accounts and
  Clubs often don't hold such data, so this blocks the common case to serve a rare one. Rejected.
- **Accept duplicates now, merge later (chosen).** Keeps data entry honest and local to each Club;
  pushes the genuinely hard identity-resolution problem to a deliberate, person-driven step.

## Consequences

**Positive**
- No accidental identity fusion at entry; a Club's roster only ever reflects what that Club typed.
- Transfer candidates are both safer and more useful — scoped to shared Leagues and shown with the
  context (league, discipline, current club) needed to tell two real people apart.

**Negative**
- The same person genuinely known to several Clubs exists as several Player records until merged —
  more rows, and night/league stats don't span those records.
- An identity-merge feature is now a prerequisite for any cross-Club "this is the same person" view.
  This ADR records that the gap is intentional, not an oversight.

Complements [ADR-0003](0003-player-discipline-registration.md), which fixed registration scope and
the three-party transfer; this ADR fixes how players enter the system and what a transfer may search.
