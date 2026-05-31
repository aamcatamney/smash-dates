# 3. Player discipline registration is league-scoped, with three-party transfers

Date: 2026-05-30

## Status

Accepted

## Context

We are introducing Players. A Player is a global, admin-managed person record (no login) who can be affiliated with several Clubs. The substantive new rule is **registration**: a Player is registered to play a **Discipline** (`Level` or `Mixed`) for a Club, and a League must govern that registration — confirming new ones and adjudicating moves between Clubs ("transfers").

The open question was the *scope* of a registration and therefore where exclusivity lives and who governs a transfer. A Club can belong to more than one League, so "the league confirms it" is ambiguous unless scope is pinned down.

Alternatives considered:

- **Club-global registration** `(Player, Club, Discipline)` — a Player plays a discipline for exactly one Club, period. Simplest data, but when a Club is in multiple Leagues it is unclear which League's admin confirms or can block a transfer; governance has no single owner.
- **Club-global, all Leagues confirm** — every League the Club belongs to must approve. Strongest governance but an N-League sign-off workflow that is heavy and surprising.
- **League-scoped registration** `(Player, Club, League, Discipline)` — the registration names the League, so the confirming authority is unambiguous and exclusivity is naturally per `(Player, League, Discipline)`.

## Decision

A Discipline Registration is scoped to **`(Player, Club, League, Discipline)`**.

- Eligibility: the Player must be a **Member** (not Visitor) of the Club, and the Club must be an Accepted member of the League.
- Lifecycle `Pending → Confirmed | Rejected`: a `ClubAdmin@Club` requests; a `LeagueAdmin@League` confirms or rejects.
- **Exclusivity** is enforced per `(Player, League, Discipline)`: at most one `Confirmed` registration. Confirming a second Club for the same triple is rejected (409) — the only way to move it is a transfer.
- **Transfer** moves a `Confirmed` registration to another Club within the same `(League, Discipline)`. The **receiving** Club initiates (its request is its agreement); the **releasing** `ClubAdmin@Club` and the `LeagueAdmin@League` must both approve. All three agreeing moves the registration (the receiving Club gains a `Member` affiliation if needed); any rejection cancels it.

Registrations in different Leagues, or in the other Discipline, are independent.

## Consequences

**Positive**
- "The league confirms / governs the transfer" has exactly one owner per registration — no ambiguity when a Club plays in several Leagues.
- Exclusivity and the transfer workflow fall out of the scope naturally (a partial unique index on `(player, league, discipline)` where status = Confirmed).
- A Player can legitimately play different disciplines for different Clubs, and play in multiple Leagues, without special cases.

**Negative**
- A Player who genuinely plays for one Club across two Leagues must be registered (and confirmed) once per League — more rows and more confirmations than a club-global model.
- Exclusivity is per discipline, not per league: the model permits a Player to be registered for `Level` at one Club and `Mixed` at another within the same League. If a League ever needs "one club per player, all disciplines", that is a stricter rule to add later, not the default here.

**Mitigation**
- The partial unique index makes the exclusivity rule a database invariant, not just application logic.
- Transfers preserve history as their own records, so a registration's movement between Clubs is auditable.
