# smash-dates

.NET 10 web app serving an Angular client. Backend uses Dapper + Npgsql against PostgreSQL with SQL migrations applied on startup via DbUp.

## Stack

- .NET 10 (single project, `smash-dates.csproj`)
- PostgreSQL
- Dapper + Npgsql (repository pattern, no EF Core)
- DbUp for migrations (embedded `Migrations/Scripts/*.sql`)
- BCrypt.Net-Next for password hashing
- Cookie authentication (ASP.NET Core), Data Protection keys persisted in Postgres
- Angular 21 client in `ClientApp/`, served as static files

## Prerequisites

- .NET 10 SDK
- Docker (or a local Postgres instance)
- Node.js + npm (for the Angular client)

## Configuration

Connection string is read from `ConnectionStrings:Postgres`. Default in `appsettings.json` points at `localhost:5432` as `postgres` / `postgres`. Override via env var:

```
ConnectionStrings__Postgres=Host=db;Port=5432;Database=smash_dates;Username=...;Password=...
```

## Running locally

Start Postgres via Docker Compose:

```powershell
docker compose up -d
```

Tear down (keep data):

```powershell
docker compose down
```

Tear down and wipe the volume:

```powershell
docker compose down -v
```

Install Angular dependencies (first run):

```powershell
cd ClientApp
npm install
cd ..
```

### Dev loop

Two terminals, both same-origin (Angular output served from `wwwroot` by .NET â€” no `ng serve`, no proxy):

```powershell
# Terminal 1 â€” rebuild Angular on change into dist/
cd ClientApp
npm run watch

# Terminal 2 â€” run API (also serves the Angular bundle and SPA fallback)
dotnet run
```

On startup the app ensures the database exists, then DbUp applies any pending scripts from `Migrations/Scripts/` and tracks them in the `schemaversions` table. Re-running is idempotent.

### Production build

```powershell
cd ClientApp
npm run build
cd ..
dotnet run
```

### Tests

```powershell
# Backend (integration tests need Docker running for Testcontainers)
dotnet test

# Frontend
cd ClientApp
npm test
```

## Foundation slice

The first vertical slice exposes the bootstrap admin surface for the league-scheduling domain:

- `POST /api/auth/register` — first registered user is automatically promoted to **SystemAdmin** (other users default to non-admin).
- `POST /api/leagues` *(SystemAdmin)* — create a League.
- `GET  /api/leagues` *(authenticated)* — list Leagues.
- `GET  /api/leagues/{id}` *(authenticated)* — get one League.
- `POST /api/leagues/{leagueId}/divisions` *(LeagueAdmin@thisLeague | SystemAdmin)* — create a Division (with `gender`, `rank`, `rubbersPerMatch`, `winPoints`/`drawPoints`/`lossPoints`). (Originally SystemAdmin-only; broadened in slice 2a.)
- `GET  /api/leagues/{leagueId}/divisions` *(authenticated)* — list Divisions.

Frontend routes (Angular, lazy-loaded under `/admin`, gated by `systemAdminGuard`):

- `/admin/leagues` — list + create form.
- `/admin/leagues/:id` — detail with divisions list + create form.

Subsequent slices will add Clubs, Teams, Venues, Seasons, Weeks, Blocked Dates, the LeagueAdmin/ClubAdmin role grants, and the auto-scheduler.

The domain glossary lives in [CONTEXT.md](CONTEXT.md); architectural decisions are recorded under [docs/adr/](docs/adr/).

## Slice 2a — League admin grants

- `POST /api/leagues` *(SystemAdmin)* — body now requires `firstLeagueAdminUserId`. League + first admin grant are created atomically.
- `GET  /api/leagues/{id}/admins` *(authenticated)*
- `POST /api/leagues/{id}/admins` *(LeagueAdmin@thisLeague | SystemAdmin)* — body `{ userId }`. Idempotent.
- `DELETE /api/leagues/{id}/admins/{userId}` *(LeagueAdmin@thisLeague | SystemAdmin)* — last-admin removal returns 409 unless caller is SystemAdmin.
- `POST /api/leagues/{leagueId}/divisions` *(LeagueAdmin@thisLeague | SystemAdmin)* — previously SystemAdmin-only.
- `GET  /api/users/lookup?email=...` *(authenticated)* — resolves email → userId for granter UIs.

Frontend route added: `/admin/leagues/:id/admins`. Create-league form now also requires the first admin's email.

## Slice 2b — Clubs + ClubAdmin + Memberships

- `POST   /api/clubs` *(SystemAdmin)* — atomically creates a Club and the first ClubAdmin grant. Body: `name`, `shortCode` (3-5 chars), `contactEmail`, optional `notes`, `firstClubAdminUserId`.
- `GET    /api/clubs` *(authenticated)* — open registry.
- `GET    /api/clubs/{id}` *(authenticated)*.
- `PATCH  /api/clubs/{id}` *(ClubAdmin@thisClub | SystemAdmin)*.
- `GET    /api/clubs/{id}/admins` *(authenticated)*.
- `POST   /api/clubs/{id}/admins` *(ClubAdmin@thisClub | SystemAdmin)* — body `{ userId }`. Idempotent.
- `DELETE /api/clubs/{id}/admins/{userId}` *(ClubAdmin@thisClub | SystemAdmin)* — last-admin rule mirrors LeagueAdmin.
- `POST   /api/leagues/{leagueId}/memberships` *(LeagueAdmin@thisLeague | SystemAdmin)* — body `{ clubId }`. Creates Pending.
- `GET    /api/leagues/{leagueId}/memberships` *(authenticated)*.
- `GET    /api/clubs/{clubId}/memberships` *(authenticated)*.
- `POST   /api/leagues/{leagueId}/memberships/{id}/accept` *(ClubAdmin@thatClub)*.
- `POST   /api/leagues/{leagueId}/memberships/{id}/decline` *(ClubAdmin@thatClub)*.
- `POST   /api/leagues/{leagueId}/memberships/{id}/withdraw` *(ClubAdmin@thatClub)*.
- `POST   /api/leagues/{leagueId}/memberships/{id}/expel` *(LeagueAdmin@thisLeague | SystemAdmin)*.

Frontend additions: `/admin/clubs` (list + create when SystemAdmin), `/admin/clubs/:id` (detail with admin management + memberships). League detail gains a member-clubs section with invite + expel. The `/admin` route's `systemAdminGuard` is loosened to plain `authGuard` so ClubAdmins can reach their own pages; per-action authorisation remains server-enforced.

The mid-season Withdraw/Expel block (per CONTEXT.md) is **deferred** until Seasons + Season Entries land.

## Slice 2c — Teams + Venues

Club-owned assets, both nested under a Club. Reads are open to any authenticated user (Clubs are an open registry); writes require `ClubAdmin@thisClub | SystemAdmin`.

Teams:

- `POST   /api/clubs/{clubId}/teams` *(ClubAdmin@thisClub | SystemAdmin)* — body `{ name, gender }` (`Mens`|`Ladies`|`Mixed`). Name unique per Club (case-insensitive).
- `GET    /api/clubs/{clubId}/teams` *(authenticated)*.
- `PATCH  /api/clubs/{clubId}/teams/{id}` *(ClubAdmin@thisClub | SystemAdmin)* — body `{ name }`. **Gender is immutable** after creation.
- `DELETE /api/clubs/{clubId}/teams/{id}` *(ClubAdmin@thisClub | SystemAdmin)*.

Venues:

- `POST   /api/clubs/{clubId}/venues` *(ClubAdmin@thisClub | SystemAdmin)* — body `{ name, capacity }` (`capacity` 1 or 2, defaults to 1). Name unique per Club (case-insensitive).
- `GET    /api/clubs/{clubId}/venues` *(authenticated)*.
- `PATCH  /api/clubs/{clubId}/venues/{id}` *(ClubAdmin@thisClub | SystemAdmin)* — body `{ name, capacity }`.
- `DELETE /api/clubs/{clubId}/venues/{id}` *(ClubAdmin@thisClub | SystemAdmin)*.

Delete is a **guarded hard delete**: allowed while unreferenced. Referential guards (409 once a Team has a Season Entry / a Venue has hosted a Match) will be added when those tables land.

Venue **unavailable dates** (the `VenueBlocked` scope) are **deferred** to the Blocked Dates slice — see CONTEXT.md.

Frontend additions: the `/admin/clubs/:id` detail page gains **Teams** and **Venues** sections (list + create + delete), matching the existing admin/membership management style.

## Slice 2d — Seasons + Weeks

A Season belongs to a League and owns an ordered list of Weeks. This slice covers **Draft-state CRUD only**; lifecycle transitions (`Scheduling`, `Proposed`, `Active`, `Closed`) arrive with the scheduler. Reads are open to any authenticated user; writes require `LeagueAdmin@thisLeague | SystemAdmin`.

- `POST   /api/leagues/{leagueId}/seasons` *(LeagueAdmin@thisLeague | SystemAdmin)* — creates a Season (status `Draft`) **and its Weeks atomically**. Body: `name` (unique per League, case-insensitive), `startDate`, `endDate`, `weeks` (may be empty).
- `GET    /api/leagues/{leagueId}/seasons` *(authenticated)*.
- `GET    /api/leagues/{leagueId}/seasons/{id}` *(authenticated)* — includes the Week list, ordered by start date.
- `PUT    /api/leagues/{leagueId}/seasons/{id}/weeks` *(LeagueAdmin@thisLeague | SystemAdmin)* — **replaces** the whole Week list. Allowed only while `Draft` (else 409).
- `DELETE /api/leagues/{leagueId}/seasons/{id}` *(LeagueAdmin@thisLeague | SystemAdmin)* — allowed only while `Draft` (else 409).

Each Week is `{ startDate, endDate, weekType }` with `weekType` ∈ `{ Level, Mixed }`. Week validation (enforced on create and replace): `startDate ≤ endDate`, weeks within the Season's date range, and **non-overlapping**. Gaps are allowed (omit weeks). Week order is derived from `startDate`, not stored — see [docs/adr/0002](docs/adr/0002-weeks-ordered-by-date.md).

`DateOnly` is bound through a Dapper `TypeHandler` registered at startup (`Data/DateOnlyTypeHandler.cs`), since this Dapper version won't bind `DateOnly` parameters directly.

Frontend additions: the `/admin/leagues/:id` detail page gains a **Seasons** section — list (name · dates · status), create (name + start + end), delete (Draft), and an inline **Weeks** editor (add/remove rows, save = replace-all) for Draft seasons.

## Slice 2e — Season Entries

A Season Entry assigns a Team to a Division for a Season (the per-Season placement that lets Teams promote/relegate without losing identity). Writes are `Draft`-only and require `LeagueAdmin@thisLeague | SystemAdmin`; reads are open to any authenticated user.

- `POST   /api/leagues/{leagueId}/seasons/{seasonId}/entries` *(LeagueAdmin@thisLeague | SystemAdmin)* — body `{ teamId, divisionId }`.
- `GET    /api/leagues/{leagueId}/seasons/{seasonId}/entries` *(authenticated)* — includes division + team names.
- `DELETE /api/leagues/{leagueId}/seasons/{seasonId}/entries/{id}` *(LeagueAdmin@thisLeague | SystemAdmin)*.

Validation on create:

- Season must be `Draft` (else 409) — `Draft` is the team-assignment phase.
- Division must belong to the Season's League (else 404).
- Team's gender must match the Division's gender (else 400).
- The Team's Club must hold an **Accepted** membership in the League (else 409).
- A Team may be entered in at most one Division per Season — `UNIQUE (season_id, team_id)`, dup → 409.
- "Change division" = delete + re-create; there is no PATCH.

This slice also activates the **Team delete guard** deferred in slice 2c: `season_entries.team_id` is `ON DELETE RESTRICT` and `DELETE /api/clubs/{clubId}/teams/{id}` now returns 409 when the Team is assigned to any Season.

Frontend additions: the Season panel on `/admin/leagues/:id` gains a **Teams** toggle — list current entries (team → division), assign (team picker drawn from the league's Accepted-member clubs + division picker), and remove.

## Slice 2f — Blocked Dates

A Blocked Date is a `(startDate, endDate, reason)` range during which one scope can't host or play. All three scopes are owned by the Club admin, so they live in one collection under the Club. Writes require `ClubAdmin@thisClub | SystemAdmin`; reads are open to any authenticated user.

- `POST   /api/clubs/{clubId}/blocked-dates` *(ClubAdmin@thisClub | SystemAdmin)* — body `{ scope, venueId?, teamId?, startDate, endDate, reason }`.
- `GET    /api/clubs/{clubId}/blocked-dates` *(authenticated)* — every scope for the club.
- `DELETE /api/clubs/{clubId}/blocked-dates/{id}` *(ClubAdmin@thisClub | SystemAdmin)*.

`scope` ∈ `{ Club, Venue, Team }`:

- **Club** — no Team of the Club plays (AGM, social night). No `venueId`/`teamId`.
- **Venue** — a Venue is unavailable. `venueId` required; the Venue must belong to the Club (else 404).
- **Team** — a Team can't play. `teamId` required; the Team must belong to the Club (else 404).

Validation: `reason` required, `startDate ≤ endDate`, valid `scope`. Single-day blocks use `startDate == endDate`. Overlaps are allowed. A DB CHECK ties exactly the matching FK to each scope. Edit = delete + re-create (no PATCH).

**Deferred:** the lifecycle lock (CONTEXT.md: blocks forbidden once a Season is `Active`) is **not** enforced yet. Blocked Dates are Club/Venue/Team calendar facts with no Season anchor — a Club can be in many Leagues at once, so "which Season's Active state gates this block" has no clean answer until the scheduler and Active transitions exist. This mirrors the deferral of the mid-season Withdraw/Expel block.

Frontend additions: the `/admin/clubs/:id` detail page gains a **Blocked dates** section — list (scope · target · dates · reason), create (scope selector revealing a Venue or Team picker, from/to dates, reason), and delete.

## Adding a migration

Create `Migrations/Scripts/NNNN_description.sql` (zero-padded sequence). The file is automatically included as an embedded resource. DbUp applies scripts in name order on next startup.

## License

MIT â€” see [LICENSE](LICENSE).

