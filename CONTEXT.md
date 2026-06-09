# Smash Dates — Domain Glossary

> Living document. Captures the canonical language of the badminton-league scheduling domain. No implementation detail.

## Roles and Access

- **User** — global account (email + password).
- **SystemAdmin** — bootstrap role. Creates Leagues; assigns first LeagueAdmin per League. First registered user becomes SystemAdmin.
- **LeagueAdmin@League** — per-League role-grant. Manages Divisions, invites Clubs into the League, manages Teams' Season assignments, configures Seasons + Weeks, runs the scheduler, force-confirms Matches. Many Users can hold the LeagueAdmin grant for the same League; any holder can grant or revoke the role for another User. A League must always have at least one LeagueAdmin (last-admin removal rejected with 409 unless the caller is `SystemAdmin`, who may force the League adminless pending re-bootstrap). A LeagueAdmin may resign as long as another LeagueAdmin remains. League-scoped endpoints (Division CRUD, Season setup, etc.) authorize either `LeagueAdmin@<thisLeague>` **or** `SystemAdmin`.
- **ClubAdmin@Club** — per-Club role-grant. Manages Venues, enters blocked dates (Club-wide and per-Team), accepts/rejects proposed Matches for the Club. Many Users can hold the ClubAdmin grant for the same Club; any holder can grant or revoke the role for another User. A Club must always have at least one ClubAdmin (the last-admin removal is rejected with 409 unless the caller is `SystemAdmin`, who may force the Club into an adminless state pending re-bootstrap). A ClubAdmin may resign as long as another ClubAdmin remains.
- **SessionHost@Club** — per-Club role-grant for running [Pegboard Sessions](#pegboard-session). Granted and revoked by any `ClubAdmin@Club`. A SessionHost may open, run and close a Club's club-night sessions, but holds **no other** Club powers (cannot manage Venues, Teams, Players, etc.). Running a session is authorized for `SessionHost@<thisClub>` **or** `ClubAdmin@<thisClub>` **or** `SystemAdmin` (ClubAdmin is implicitly a host). There is **no last-host protection** — a Club may have zero SessionHosts, since its admins can always run sessions.
- Authenticated Users may **read** any League's schedule, and any authenticated User may **view** a running Pegboard Session (read-only). There is no `Player` **login** role (Players are admin-managed roster records — see [Player](#player)).
- **Anonymous public view** — a logged-out visitor may read a League's published **standings** and **fixture list** (per Season). It is strictly read-only and **PII-free**: only team / division / venue names, dates and scores are exposed — never Club contact details, membership state, or any admin data. Only Seasons that have a schedule (`Proposed` / `Active` / `Closed`) are shown.
- One User may hold many role-grants simultaneously (e.g. LeagueAdmin of League A + ClubAdmin of Club B).

## Terms

### Blocked Date
A `(StartDate, EndDate, Reason)` range during which one of three scopes cannot host or play Matches. Single-day blocks are stored as a range with `StartDate == EndDate`. `Reason` is required free text.

Three scopes, all owned by the relevant Club admin:
- **VenueBlocked** — venue unavailable (other booking, maintenance).
- **ClubBlocked** — no Team of the Club plays (club AGM, social night).
- **TeamBlocked** — this Team cannot play (player exams, holiday).

### Venue
A physical hall belonging to a Club. A Club has 1..N Venues, all interchangeable from the scheduler's perspective. Each Venue has:
- **Courts** — the number of physical badminton courts in the hall (1..N).
- **MaxConcurrentMatches** — the Venue's own ceiling (`1` or `2`) on how many Matches may run at once, regardless of how many courts it has.
- an optional **Address** — free text, shown with an external map link (display/navigation aid only; the scheduler ignores it).
- a list of **unavailable dates** (the [VenueBlocked](#blocked-date) scope — not a separate concept).

The number of Matches a Venue can actually host simultaneously in one `(Venue, Date)` slot is **derived**, not stored: `min(MaxConcurrentMatches, ⌊Courts ÷ CourtsPerMatch⌋)`, where [CourtsPerMatch](#scheduling-constraints) is a per-League rule. A Match occupies more than one court (its rubbers run in parallel), so a hall needs enough courts *and* a high enough concurrency ceiling to run two Matches at once.

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

A `(Venue, Date)` slot may host more than one Match simultaneously, up to the Venue's derived slot capacity (see [Venue](#venue)).

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
- A `(Venue, Date)` slot hosts no more than its derived slot capacity — `min(MaxConcurrentMatches, ⌊Courts ÷ CourtsPerMatch⌋)` (see [Venue](#venue)).
- A Venue cannot host on its unavailable dates.
- A Team cannot play on its Club's blocked dates or the Team's own blocked dates.
- Derby-first rule (see [Derby](#derby)).
- Every Team plays every other Team in its Division home and away (double round-robin).
- Matches land only in Weeks of the matching WeekType for the Division's gender.

**Soft (scheduler optimises, penalty-weighted):**
- Minimise back-to-back / closely-spaced Matches for any one Team.
- Maximise gap between the home leg and the away leg of the same pairing (target ≈ half season length).

Penalty weights and target gap values are **per-League configuration** with sensible defaults. **CourtsPerMatch** — how many courts one Match occupies (its rubbers run in parallel) — is also per-League, defaulting to `2`; it feeds the Venue slot-capacity derivation above.

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
A persistent person record, **global** (not owned by a single Club), so the same Player can be affiliated with several Clubs. Created and edited by any `ClubAdmin` of a Club the Player is affiliated with. Attributes: a **FullName**, a **Gender** (`Male` | `Female`), and an optional **Grade** (`1`–`5`). FullName and Grade are editable; **Gender is immutable** after creation. Because the record is global, a rename shows everywhere the Player is affiliated. A Player is **not** a login account — there is no Player role; Players are managed entirely by Club admins.

Gender exists so the [Level discipline](#discipline) resolves to the right gendered play: a `Male` Player playing Level is a Mens player, a `Female` Player playing Level is a Ladies player.

**Grade** is an optional ability rating, `1` (strongest) to `5` (weakest), used only to balance [Games](#game) when a [Pegboard Session](#pegboard-session) suggests or auto-fills a court. It is purely a club-night aid and has no bearing on Leagues, Seasons, scheduling or standings.

Player rosters follow the same open-registry read model as Clubs (any authenticated user may read a Club's roster; only its admins write). A Club admin adding a Player to their roster always creates a **new** Player record — there is **no by-name lookup of Players across other Clubs**. A person known to several Clubs is therefore held as a separate Player record per Club until a future **identity merge** reconciles them. The one cross-Club path is a [Registration Transfer](#registration-transfer), whose candidate search is scoped to Leagues the receiving Club is an Accepted member of.

### Player–Club Affiliation
A link between a Player and a Club with a **type**: `Member` or `Visitor`.
- **Member** — the Club may **register** this Player for [disciplines](#discipline-registration). A Player may be a Member of more than one Club.
- **Visitor** — the Player is listed at this Club as a guest only; their registration lives at the Club where they are a Member. A Visitor cannot be registered by this Club.

Set by the Club when adding the Player. A Club admin adding a Player always creates a **new** Player record plus this affiliation; there is no link-an-existing-Player step at the Club level. A second affiliation to an **existing** Player arises only when a [Registration Transfer](#registration-transfer) lands at a Club lacking a Member affiliation (the Club gains one) — or, in future, when separate Player records are merged.

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
A Team's persistent list of Players, managed by a `ClubAdmin@Club`. A Player may be added to a Team's squad only if **eligible**: they hold a `Confirmed` [Discipline Registration](#discipline-registration) at the Team's Club for the Team's discipline (`Mens`/`Ladies` Team → `Level`, `Mixed` Team → `Mixed`), and — for a `Level` Team — a matching gender (`Male` → Mens Team, `Female` → Ladies Team). Eligibility is **not** tied to which Leagues the Team is entered in, so a squad can be built before the Team is entered in a Season. Ineligible adds are rejected (409). The squad is the roster only — Matches remain atomic ties with no per-Match lineup.

## Club Night (Pegboard)

A separate, in-person concern from league play. A Pegboard Session is the digital replacement for the physical pegboard a Club uses on a social/practice night to track who turned up, who's waiting, the courts, and who's playing on them. It is **entirely disconnected** from Leagues, Seasons, Matches and Standings — no result here affects any league table.

### Pegboard Session
A single club night, owned by one [Club](#club). Has a **Name** (or date) and a status: `Scheduled → Open → Closed`.

- Opened, run, scheduled and closed by a [SessionHost@Club](#roles-and-access) (or ClubAdmin / SystemAdmin). A Club has **at most one `Open` Session at a time**.
- **Scheduled** is the optional leading state: a session planned ahead of time. It carries a **ScheduledDate** (required) and an optional **StartTime**, **Duration** and **Venue** — these are informational ("when and where"); they do not auto-open or auto-close the session. A Club may have **any number** of Scheduled sessions queued. A host **opens** a Scheduled session when the night begins (`Scheduled → Open`), subject to the one-`Open`-per-club rule. A Scheduled session may be edited or deleted while still Scheduled; once Open it follows the normal lifecycle.
- Holds the night's [Courts](#court), [Attendances](#attendance) and [Games](#game) (only from when it is Open).
- **Closing** ends any in-progress Games with no result recorded and makes the board read-only. `Closed` is terminal (no reopen) and retained as history — viewable, not editable.
- Optionally references a [Venue](#venue) as its location, but is **not** pinned to it: the Club still picks how many Courts to run on the night (a Venue's court count does not seed the board).
- Any authenticated User may **view** a running Session; only the host (or admin) may mutate it. The board updates live for all viewers.

### Court
A playing court within a [Pegboard Session](#pegboard-session). The host **adds** Courts at any time and **removes** a Court only while it is empty (no active Game). A Court hosts at most one active [Game](#game) at a time.

### Attendance
One person's presence at a [Pegboard Session](#pegboard-session) — the digital "peg". Each Attendance is **either** a roster [Player](#player) (a Player affiliated to the Club, `Member` or `Visitor`) **or** an ad-hoc **guest** (free-text `Name` + `Gender` + optional `Grade`), never both. A guest is not persisted as a Player. An Attendance carries an optional **Grade** (`1`–`5`): copied from the Player's [Grade](#player) on add but editable for the night without changing the Player record.

The host adds an attendee in one of two ways: **pick an existing roster Player** (the common case), or **register a walk-in** — which creates a real Player with a `Visitor` affiliation to the Club and adds them, so they are remembered for next time. Registering a walk-in this way is authorized for the session runner (host or admin), not only ClubAdmin — see [ADR-0010](docs/adr/0010-session-host-registers-walk-in-visitors.md). (The ephemeral guest path remains in the model but is no longer the primary flow.)

Status: `Waiting | Playing | Resting | Left`.
- **Waiting** — in the queue, available to be picked. The queue is ordered by time entered `Waiting` (longest-waiting first).
- **Playing** — currently on a Court in an active Game.
- **Resting** — present but paused out of the queue (a self-imposed break); not picked until they rejoin.
- **Left** — gone for the night; excluded from the queue and not pickable.

Finishing or cancelling a Game returns its players to the queue **tail** (`Waiting`). The host may move anyone between states.

### Game
A single match played on one [Court](#court) during a [Pegboard Session](#pegboard-session). Has a **Game Type**, two **Sides** (A and B), a status `Active → Finished`, and — once finished — a required **WinnerSide** and an optional free-text **Score** (e.g. `21-15`).

- **Game Type** is `Singles | Doubles | Mixed | Funny`:
  - **Singles** — 1 player per Side (2 total).
  - **Doubles** — 2 per Side, all four the same gender (level: Mens or Ladies).
  - **Mixed** — 2 per Side, each Side one `Male` + one `Female`.
  - **Funny** — 2 per Side, any other gender arrangement (e.g. `3+1`, or a Mens pair vs a Ladies pair). The catch-all for non-standard social games.
- A makeup that breaks the Type's gender rule produces a **warning** only — the host has final say and may start it anyway.
- An active Game may be **Cancelled** (e.g. started by mistake): its players return to the queue with no win/loss recorded — distinct from **Finishing**, which requires a WinnerSide.
- **Night stats** are derived per Attendance from finished Games: games played, games won, and wait time. Session-scoped only.

### Side
One of the two teams in a [Game](#game) (Side A / Side B), each holding 1 player (Singles) or 2 players (all other Types). The WinnerSide of a finished Game names the side that won.

### Board Fill Modes
How the host fills a free [Court](#court) from the [Waiting](#attendance) queue. Chosen **per fill**, not locked for the Session:
- **Manual** — host picks the players directly.
- **Suggest** — the board proposes a valid set; the host confirms or swaps before starting.
- **Auto-rotate** — the board fills the Court itself.

Suggest and Auto-rotate balance, in priority: fairness (longest-waiting / fewest games first), valid makeup for the chosen Type, partner/opponent variety against earlier Games that night, and ability balance using attendee [Grade](#player).
