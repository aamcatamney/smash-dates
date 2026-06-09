# Screenshots

Screenshots embedded in the root `README.md`, captured from a seeded demo league:

| File | Shows |
|------|-------|
| `leagues.png` | Leagues list (`/leagues`) |
| `league-detail.png` | League detail — divisions, seasons, member clubs |
| `season-setup.png` | Season weeks editor + team entries |
| `fixtures.png` | A generated season's fixtures |
| `match-actions.png` | Confirm / reject / record-result controls on fixtures |
| `standings.png` | Division standings table |
| `club-detail.png` | Club detail — teams, venues, blocked dates, matches |
| `csv-import.png` | Bulk CSV import dialog with a per-row result |
| `dark-mode.png` | League detail in the dark theme |
| `players.png` | Player registrations + transfers awaiting league approval |
| `profile.png` | Profile page (`/profile`) — change password + read-only role grants |
| `public-standings.png` | Anonymous public view (`/public`) — a league's standings + fixtures, no login |
| `pegboard-sessions.png` | Club page → Sessions tab: past/current club nights + "Open session" |
| `pegboard-board.png` | Full-screen live pegboard board — courts grid + waiting queue |

Dark-theme variants `pegboard-sessions-dark.png`, `pegboard-board-dark.png` and `public-standings-dark.png` are also kept.

Suggested width ~1400px, PNG.

## Seeding the demo

[`scripts/seed-demo.sh`](../../scripts/seed-demo.sh) populates a fresh instance with the demo
league these screenshots are taken from — a league with two divisions, four clubs (each with a
venue, a team and an accepted membership), players + discipline registrations (some confirmed
into a team squad), a fully generated and partly-played season (so standings and fixtures have
content), a second season left in Draft (the weeks/entries setup view), and an open pegboard club
night with a finished game. It drives the real HTTP API as the bootstrap SystemAdmin.

```bash
# 1. Start a FRESH database + the app (build the client first so the SPA is served):
cd ClientApp && npm run build && cd ..
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=smash_dates;Username=postgres;Password=postgres" \
  ASPNETCORE_ENVIRONMENT=Development dotnet run &
# 2. Seed it (defaults to http://localhost:5080):
scripts/seed-demo.sh
# then sign in as admin@smash-dates.test / correct-horse-battery and capture.
```

Requires `bash`, `curl` and `python3`, and an empty database (it registers the first user, who
becomes the SystemAdmin). `BASE_URL`, `ADMIN_EMAIL` and `ADMIN_PASSWORD` are overridable via env.
