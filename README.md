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
- `POST /api/leagues/{leagueId}/divisions` *(SystemAdmin)* — create a Division (with `gender`, `rank`, `rubbersPerMatch`, `winPoints`/`drawPoints`/`lossPoints`).
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

## Adding a migration

Create `Migrations/Scripts/NNNN_description.sql` (zero-padded sequence). The file is automatically included as an embedded resource. DbUp applies scripts in name order on next startup.

## License

MIT â€” see [LICENSE](LICENSE).

