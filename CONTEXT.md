# Smash Dates — Domain Glossary

> Living document. Captures the canonical language of the badminton-league scheduling domain. No implementation detail.

## Roles and Access

- **User** — global account (email + password).
- **SystemAdmin** — bootstrap role. Creates Leagues; assigns first LeagueAdmin per League. First registered user becomes SystemAdmin.
- **LeagueAdmin@League** — per-League role-grant. Manages Divisions, invites Clubs into the League, manages Teams' Season assignments, configures Seasons + Weeks, runs the scheduler, force-confirms Matches. Many Users can hold the LeagueAdmin grant for the same League; any holder can grant or revoke the role for another User. A League must always have at least one LeagueAdmin (last-admin removal rejected with 409 unless the caller is `SystemAdmin`, who may force the League adminless pending re-bootstrap). A LeagueAdmin may resign as long as another LeagueAdmin remains. League-scoped endpoints (Division CRUD, Season setup, etc.) authorize either `LeagueAdmin@<thisLeague>` **or** `SystemAdmin`.
- **ClubAdmin@Club** — per-Club role-grant. Manages Venues, enters blocked dates (Club-wide and per-Team), accepts/rejects proposed Matches for the Club. Many Users can hold the ClubAdmin grant for the same Club; any holder can grant or revoke the role for another User. A Club must always have at least one ClubAdmin (the last-admin removal is rejected with 409 unless the caller is `SystemAdmin`, who may force the Club into an adminless state pending re-bootstrap). A ClubAdmin may resign as long as another ClubAdmin remains.
- Authenticated Users may **read** any League's schedule. There is no `Player` **login** role (Players are admin-managed roster records — see [Player](#player)) and no anonymous public view.
- One User may hold many role-grants simultaneously (e.g. LeagueAdmin of League A + ClubAdmin of Club B).

## Terms

### Blocked Date
A `(StartDate, EndDate, Reason)` range during which one of three scopes cannot host or play Matches. Single-day blocks are stored as a range with `StartDate == EndDate`. `Reason` is required free text.

Three scopes, all owned by the relevant Club admin:
- **VenueBlocked** — venue unavailable (other booking, maintenance).
- **ClubBlocked** — no Team of the Club plays (club AGM, social night).
- **TeamBlocked** — this Team cannot play (player exams, holiday).

### Venue
A physical hall belonging to a Club. A Club has 1..N Venues, all interchangeable from the scheduler's perspective. Each Venue has a court **capacity** of 1 or 2 simultaneous Matches per slot, and a list of **unavailable dates** (the [VenueBlocked](#blocked-date) scope — not a separate concept).

### Match Status
Lifecycle: `Proposed → Confirmed → Played | Postponed → Rejected`.

- A newly-scheduled Match starts `Proposed`.
- Both the home Club admin **and** the away Club admin must accept for it to become `Confirmed`.
- If either rejects, the Match becomes `Rejected`. The scheduler re-runs **incrementally** — `Confirmed` Matches are locked; only `Rejected` and `Proposed` Matches are re-allocated.
- League admin may force a Match to `Confirmed` to break stalemates.
- After `Active`, a Match may be **Postponed**: ClubAdmin requests, both Clubs + LeagueAdmin must approve, then Match returns to `Proposed` and the scheduler reruns just that Match.
- A `Confirmed` Match on or after its scheduled date may be marked `Played` with `HomeScore`, `AwayScore`, `PlayedOn`. The Match has a calendar date only — kick-off time is agreed between Clubs out-of-band.
- A Match may be recorded as a **Walkover** by either side. Score awarded is the maximum for the winning side (e.g. 9–0 in a 9-rubber Division). Standings count it as a normal win; UI annotates the result with a walkover marker.

### Home Venue (for a Match)
Selected by the scheduler at scheduling time from the home Club's pool of Venues — whichever has capacity on the chosen date. No team-level or club-level fixed venue.

### Match
A **tie** between two Teams played on a single night. Composed internally of multiple rubbers (singles + doubles), but the scheduler treats a Match as one atomic unit placed on a `(Venue, Date)` slot. Rubber-level scoring is out of scope.

A `(Venue, Date)` slot may host 1 or 2 Matches simultaneously (court capacity).

### Division
A persistent bucket within a League (e.g. "Mens 1", "Mens 2", "Mixed 1"). Has:
- a fixed **gender type**: `Mens` | `Ladies` | `Mixed`,
- a rank order,
- **`RubbersPerMatch`** — number of rubbers (mini-games) contested per Match. Typical: 9 for Mens/Mixed (no draws), 6 for Ladies (draws possible at 3–3). Set by LeagueAdmin per Division.
- **`PointsScheme`** — `(WinPoints, DrawPoints, LossPoints)`. Defaults to `(2, 1, 0)` but configurable per Division.

Match `HomeScore + AwayScore` must equal `RubbersPerMatch`.

Persists across Seasons. Membership (which Teams play in it) is set per-Season.

### Standings
Materialised league table per `(Season, Division)`, refreshed on Match result entry. Columns: played, won, drawn, lost, rubbers for, rubbers against, rubber difference, points. Sort: points desc → rubber difference desc → rubbers for desc → head-to-head.

### Team
A persistent named roster belonging to one Club (e.g. "Acme Mens 1", "Acme Mixed 2"). Persists across Seasons. Has an inherent gender (`Mens`/`Ladies`/`Mixed`), fixed at creation, matching the Divisions it can play in.

### Season Entry
A per-Season assignment placing a Team into a Division for that Season. Lets Teams promote/relegate between Divisions without losing identity. A Team is entered in at most one Division per Season; its gender must match the Division's, and its Club must be an Accepted member of the League. Entries are managed only while the Season is `Draft`.

### Club–League Membership
A link between a Club and a League with one of five states: `Pending`, `Accepted`, `Declined`, `Withdrawn`, `Expelled`.

Lifecycle:
- A `LeagueAdmin@League` invites a Club, creating a `Pending` membership. Invites are rejected if a `Pending` or `Accepted` membership already exists for the same `(Club, League)` pair.
- Any `ClubAdmin@Club` may **Accept** (→ `Accepted`) or **Decline** (→ `Declined`).
- Any `ClubAdmin@Club` may **Withdraw** an `Accepted` membership (→ `Withdrawn`).
- Any `LeagueAdmin@League` may **Expel** an `Accepted` membership (→ `Expelled`).
- A `Withdrawn` / `Expelled` / `Declined` membership is terminal. Re-invites create a **new** membership row; status is never reset in place.

**Mid-season constraint:** `Withdraw` and `Expel` are blocked (409) while any of this League's Seasons is in state `Draft`, `Scheduling`, `Proposed`, or `Active` and contains at least one Team belonging to this Club via a Season Entry. Membership can only be ended between Seasons (after `Closed`).

### Club
A persistent organisation. Created by `SystemAdmin`. Can join many Leagues (via Club-League membership invites — League invites, Club accepts).

Attributes:
- **Name** — full name (e.g. "Acme Badminton Club").
- **ShortCode** — 3–5 character compact identifier (e.g. "ACME") used in fixture listings and the brutalist UI. ASCII letters/digits only, stored and displayed uppercase, case-insensitively unique across all Clubs.
- **ContactEmail** — primary contact address for membership invites and match-confirmation notifications when no ClubAdmin is currently signed in.
- **Notes** — free text for anything else (visible to all authenticated users; treat as a public field, not as private internal data).

Club records are an **open registry**: every authenticated User may read every Club's full record (including `ContactEmail` and `Notes`). Write access is restricted to `ClubAdmin@<thisClub>` or `SystemAdmin` (and Club creation to `SystemAdmin` only).

### Week Type
Binary attribute of each calendar week in a Season:
- **Level week** — `Mens` and `Ladies` divisions play.
- **Mixed week** — `Mixed` divisions play.

No other week types (no cup weeks, no bye weeks) at this stage.

### Scheduling Constraints

**Hard (scheduler must satisfy):**
- A Team plays at most one Match per calendar date.
- A `(Venue, Date)` slot hosts no more than its court capacity (1 or 2 Matches).
- A Venue cannot host on its unavailable dates.
- A Team cannot play on its Club's blocked dates or the Team's own blocked dates.
- Derby-first rule (see [Derby](#derby)).
- Every Team plays every other Team in its Division home and away (double round-robin).
- Matches land only in Weeks of the matching WeekType for the Division's gender.

**Soft (scheduler optimises, penalty-weighted):**
- Minimise back-to-back / closely-spaced Matches for any one Team.
- Maximise gap between the home leg and the away leg of the same pairing (target ≈ half season length).

Penalty weights and target gap values are **per-League configuration** with sensible defaults.

### Derby
A Match between two Teams from the same Club in the same Division. When a Club has N≥2 Teams in one Division, all `N*(N-1)` intra-club Matches (each pair home+away) must be scheduled **before** any of those Teams plays any other Team in the Division. Hard scheduler constraint.

### Season Lifecycle
States: `Draft → Scheduling → Proposed → Active → Closed`.
- `Draft` — admin configures Weeks, assigns Teams to Divisions, clubs accept League membership and enter blocked dates.
- `Scheduling` — LeagueAdmin clicks **Generate Schedule**; scheduler runs asynchronously as a background job.
- `Proposed` — schedule exists; clubs accept/reject Matches; LeagueAdmin may force-confirm.
- `Active` — first Match date reached; blocked-date editing locked.
- `Closed` — season end date passed.

Blocked dates may be added freely while Season is `Draft` or `Proposed`. From `Active` onward, blocked-date additions are forbidden.

### Season
Belongs to one League. Has a **Name** (a human handle, e.g. "2025/26", unique within its League), a start date, an end date, and an **explicit ordered list of Weeks**. Week order is derived from each Week's `StartDate` (Weeks never overlap), not a separate ordinal. Each Week has `(StartDate, EndDate, WeekType)` — a calendar range (typically Mon–Sun) within which Matches scheduled in that week may land on any night. Admin enters the week list when creating the Season; gaps (Christmas, tournaments) are handled by simply omitting weeks from the list.

### Player
A persistent person record, **global** (not owned by a single Club), so the same Player can be affiliated with several Clubs. Created and edited by any `ClubAdmin`. Attributes: a **FullName** and a **Gender** (`Male` | `Female`). A Player is **not** a login account — there is no Player role; Players are managed entirely by Club admins.

Gender exists so the [Level discipline](#discipline) resolves to the right gendered play: a `Male` Player playing Level is a Mens player, a `Female` Player playing Level is a Ladies player.

Player rosters follow the same open-registry read model as Clubs (any authenticated user may read a Club's roster; only its admins write). The **cross-club player search** is the exception — it can enumerate people across every Club, so it is limited to `ClubAdmin` (of any Club) or `SystemAdmin`.

### Player–Club Affiliation
A link between a Player and a Club with a **type**: `Member` or `Visitor`.
- **Member** — the Club may **register** this Player for [disciplines](#discipline-registration). A Player may be a Member of more than one Club.
- **Visitor** — the Player is listed at this Club as a guest only; their registration lives at the Club where they are a Member. A Visitor cannot be registered by this Club.

Set by the Club when adding the Player. Adding an existing global Player to a second Club creates a second affiliation; it does not duplicate the Player.

### Discipline
The category of play a Player registers for. Two values, mirroring [Week Type](#week-type):
- **Level** — same-gender play; resolves to Mens or Ladies by the Player's gender.
- **Mixed** — mixed play.

A Player may hold registrations in both disciplines.

### Discipline Registration
A record that a Player is registered to play a **Discipline** for a **Club** in a specific **League** — i.e. scoped to `(Player, Club, League, Discipline)`. Requires the Player to be a **Member** of that Club and the Club to be an Accepted member of that League.

Lifecycle: `Pending → Confirmed | Rejected`.
- A `ClubAdmin@Club` requests a registration for one of its Members → `Pending`.
- A `LeagueAdmin@League` **Confirms** (→ `Confirmed`) or **Rejects** (→ `Rejected`).

**Exclusivity:** within one `(Player, League, Discipline)` there is at most one `Confirmed` registration — a Player plays a given discipline for a single Club in a given League. Confirming a second club for the same `(Player, League, Discipline)` is rejected; the only way to move it is a [Transfer](#registration-transfer). Registrations in different Leagues, or in the other Discipline, are independent.

### Registration Transfer
Moving a `Confirmed` Discipline Registration from one Club to another within the same `(League, Discipline)`. Initiated by the **receiving** Club; both the **releasing** `ClubAdmin@Club` and the `LeagueAdmin@League` must approve.

- The receiving Club's admin opens a transfer for a Player's confirmed registration; the request counts as the receiving Club's agreement.
- The releasing Club and the League each Approve or Reject.
- All three agreeing → the registration moves to the receiving Club (which gains a `Member` affiliation if it lacks one); any Rejection cancels it and the registration stays put.

### Team Squad
A Team's persistent list of Players, managed by a `ClubAdmin@Club`. A Player may be added to a Team's squad only if **eligible**: they hold a `Confirmed` [Discipline Registration](#discipline-registration) at the Team's Club, for the Team's discipline (`Mens`/`Ladies` Team → `Level`, `Mixed` Team → `Mixed`), in a League the Team is **currently entered in** (via a Season Entry in a non-`Closed` Season). For a `Level` Team the Player's gender must also match (`Male` → Mens Team, `Female` → Ladies Team). Ineligible adds are rejected (409). The squad is the roster only — Matches remain atomic ties with no per-Match lineup.
