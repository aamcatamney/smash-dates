# Club Pegboard Session Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace a club's physical pegboard with a live, in-app session that tracks attendance, a waiting queue, courts, and games on a club social/practice night.

**Architecture:** A new bounded context, wholly disconnected from league play (no Match/Season/Standings link). A `pegboard_sessions` aggregate (one Open per club) owns `pegboard_courts`, `pegboard_attendances`, and `pegboard_games` (+ `pegboard_game_players`). A new per-club `SessionHost` role grant gates running a session; viewing is open to any authenticated user. Mutations are ordinary REST POSTs that publish a content-free "board changed" event to an in-process pub/sub; viewers subscribe via Server-Sent Events (see ADR 0004). The frontend adds a "Sessions" tab to the club page plus a full-screen board route optimised for a tablet/wall display.

**Tech Stack:** .NET 10 Minimal APIs (one endpoint per file), Dapper + Npgsql repositories, DbUp SQL migrations, Angular 21 standalone + signals + Tailwind, xUnit v3 + Testcontainers (backend), Vitest (frontend).

---

## Domain references (read before starting)

- `CONTEXT.md` → section **Club Night (Pegboard)** (canonical terms: Pegboard Session, Court, Attendance, Game, Side, Board Fill Modes) and the **SessionHost@Club** role under *Roles and Access*, and **Player.Grade**.
- `docs/adr/0004-sse-for-pegboard-live-updates.md` (transport decision + in-process limit).

## Conventions to mirror (already in the codebase)

- **Endpoint**: one `public static class XxxEndpoint` per file under `Endpoints/<Area>/`, with `MapXxxEndpoint(this IEndpointRouteBuilder)` and a private `Handle`. Request/response are nested `sealed record`s. Validation returns `Results.Problem(statusCode: ..., title: ...)`. See `Endpoints/Venues/CreateVenueEndpoint.cs`.
- **Endpoint group**: a `XxxEndpoints.cs` file maps a `MapGroup("/api/...")` and calls each `Map...` extension; wired once in `Program.cs`. See `Endpoints/Venues/VenueEndpoints.cs`.
- **Club-scoped authz**: `await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, clubAdmins, ct)` returns `null` on success or a 401/403 `IResult`. We add a `SessionAuthorizer` that also accepts SessionHost. See `Services/Auth/ClubAuthorizer.cs`.
- **Repository**: `IXxxRepository` + `XxxRepository` using `IDbConnectionFactory.Create()`, Dapper `CommandDefinition`, snake_case columns mapped to PascalCase POCO `init` props, a `SelectColumns` const. Multi-statement writes open a transaction (see `ClubRepository.CreateWithFirstAdminAsync`). Registered `AddScoped` in `Program.cs`.
- **Models**: `sealed class` POCOs with `{ get; init; }` under `Models/`. Enums are `enum` files; JSON serialises enum names (configured in `Program.cs`).
- **Migrations**: `Migrations/Scripts/NNNN_description.sql`, embedded resource, applied in name order. Next number is **0026**.
- **Integration test**: `sealed class XxxTests : IntegrationTestBase`, `Seeder.CreateUserAsync/CreateClubAsync/GrantClubAdminAsync`, `Client.LoginAsSystemAdminAsync(...)`, `response.StatusCode.Should().Be(...)`. See `tests/smash-dates.IntegrationTests/Endpoints/CreateVenueEndpointTests.cs`.
- **Frontend**: standalone components, `ChangeDetectionStrategy.OnPush`, signals, `inject()`, `input()`/`output()`, native control flow, `class`/`style` bindings (no `ngClass`/`ngStyle`), Tailwind with light+dark variants, reactive forms. API service per area (`clubs.api.ts`). Routes lazy-loaded in `admin.routes.ts`.

## File structure (created / modified)

**Backend — migration**
- Create `Migrations/Scripts/0026_create_pegboard.sql`

**Backend — models** (all Create, under `Models/`)
- `PegboardSession.cs`, `SessionStatusPeg.cs` (enum `Open|Closed` — name avoids clash with existing `SeasonStatus`; call it `PegboardSessionStatus`), `PegboardCourt.cs`, `PegboardAttendance.cs`, `AttendanceStatus.cs` (enum), `PegboardGame.cs`, `GameType.cs` (enum), `GameStatus.cs` (enum), `GameSide.cs` (enum `A|B`), `PegboardGamePlayer.cs`, `SessionHostGrant.cs`
- Modify `Models/Player.cs` (add `int? Grade`)

**Backend — repositories** (Create pairs under `Repositories/`)
- `ISessionHostRepository.cs` / `SessionHostRepository.cs`
- `IPegboardRepository.cs` / `PegboardRepository.cs` (the session aggregate: sessions, courts, attendances, games, game_players, plus board read + night-stats query)
- Modify `Repositories/IPlayerRepository.cs` / `PlayerRepository.cs` (carry `Grade` through get/list/create/update)

**Backend — services**
- Create `Services/Auth/SessionAuthorizer.cs`
- Create `Services/Pegboard/IPegboardEventPublisher.cs` / `PegboardEventPublisher.cs` (in-process SSE pub/sub)
- Create `Services/Pegboard/PegboardFiller.cs` + `IPegboardFiller.cs` (suggest/auto-fill engine)
- Create `Services/Pegboard/GameMakeup.cs` (pure makeup-validation helper, shared by start-game + filler)

**Backend — endpoints** (Create under `Endpoints/...`)
- `Endpoints/SessionHosts/SessionHostEndpoints.cs` + `GrantSessionHostEndpoint.cs` + `RevokeSessionHostEndpoint.cs` + `ListSessionHostsEndpoint.cs`
- `Endpoints/Pegboard/PegboardEndpoints.cs` + per-action files: `OpenSessionEndpoint.cs`, `CloseSessionEndpoint.cs`, `GetSessionEndpoint.cs`, `ListSessionsEndpoint.cs`, `GetBoardEndpoint.cs`, `StreamBoardEndpoint.cs`, `AddCourtEndpoint.cs`, `RemoveCourtEndpoint.cs`, `AddAttendanceEndpoint.cs`, `SetAttendanceStatusEndpoint.cs`, `RemoveAttendanceEndpoint.cs`, `SuggestFillEndpoint.cs`, `StartGameEndpoint.cs`, `FinishGameEndpoint.cs`, `CancelGameEndpoint.cs`
- Modify the existing player update endpoint under `Endpoints/Players/` to accept `grade`

**Backend — wiring**
- Modify `Program.cs` (DI registrations + `Map...` calls + `using`s)

**Frontend** (under `ClientApp/src/app/features/admin/`)
- Create `pegboard.api.ts` (DTOs + `PegboardApi` service)
- Create `pegboard.store.ts` (board state + SSE subscription) — optional NgRx signal store; plan uses a lightweight signal-based service
- Create `pegboard-board.page.ts` (full-screen board route)
- Create `pegboard-sessions.component.ts` (the "Sessions" tab content)
- Modify `club-detail.page.ts` (add the Sessions tab)
- Modify `admin.routes.ts` (add board route)
- Create tests: `pegboard.api.spec.ts`, `pegboard-board.page.spec.ts`

**Docs**
- Modify `README.md` (Features + Screenshots), add screenshots under `docs/screenshots/`

---

## Phase 1 — Schema & models

### Task 1: Migration `0026_create_pegboard.sql`

**Files:**
- Create: `Migrations/Scripts/0026_create_pegboard.sql`

- [ ] **Step 1: Write the migration SQL**

Create `Migrations/Scripts/0026_create_pegboard.sql`:

```sql
-- Club night "pegboard": in-person session tracking, wholly separate from league play.
-- See docs/adr/0004-sse-for-pegboard-live-updates.md and CONTEXT.md "Club Night (Pegboard)".

-- Optional ability grade on global players (1 = strongest .. 5 = weakest). Pegboard-only aid.
ALTER TABLE players ADD COLUMN grade smallint NULL CHECK (grade BETWEEN 1 AND 5);

-- Per-club role grant: may run pegboard sessions and nothing else. No last-host protection.
CREATE TABLE session_hosts (
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    granted_at  timestamptz NOT NULL DEFAULT now(),
    granted_by  uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    PRIMARY KEY (club_id, user_id)
);
CREATE INDEX ix_session_hosts_user ON session_hosts (user_id);

-- One club night, owned by a club. Status Open -> Closed (terminal).
CREATE TABLE pegboard_sessions (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    name        text NOT NULL,
    status      text NOT NULL DEFAULT 'Open' CHECK (status IN ('Open', 'Closed')),
    opened_by   uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    opened_at   timestamptz NOT NULL DEFAULT now(),
    closed_at   timestamptz NULL
);
CREATE INDEX ix_pegboard_sessions_club ON pegboard_sessions (club_id);
-- At most one Open session per club (invariant, enforced in the database).
CREATE UNIQUE INDEX ux_pegboard_session_open_per_club
    ON pegboard_sessions (club_id) WHERE status = 'Open';

-- Courts within a session. Host adds any time; removes only while empty.
CREATE TABLE pegboard_courts (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id  uuid NOT NULL REFERENCES pegboard_sessions(id) ON DELETE CASCADE,
    label       text NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_pegboard_courts_session ON pegboard_courts (session_id);

-- Attendances (the "pegs"): a roster player OR an ad-hoc guest, never both.
CREATE TABLE pegboard_attendances (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id    uuid NOT NULL REFERENCES pegboard_sessions(id) ON DELETE CASCADE,
    player_id     uuid NULL REFERENCES players(id) ON DELETE RESTRICT,
    guest_name    text NULL,
    gender        text NOT NULL CHECK (gender IN ('Male', 'Female')),
    grade         smallint NULL CHECK (grade BETWEEN 1 AND 5),
    status        text NOT NULL DEFAULT 'Waiting'
                  CHECK (status IN ('Waiting', 'Playing', 'Resting', 'Left')),
    waiting_since timestamptz NOT NULL DEFAULT now(),
    created_at    timestamptz NOT NULL DEFAULT now(),
    CHECK ((player_id IS NULL) <> (guest_name IS NULL))
);
CREATE INDEX ix_pegboard_attendances_session ON pegboard_attendances (session_id);
-- A roster player appears at most once per session.
CREATE UNIQUE INDEX ux_pegboard_attendance_player
    ON pegboard_attendances (session_id, player_id) WHERE player_id IS NOT NULL;

-- Games on a court. Active -> Finished (needs winner) | Cancelled (no result).
CREATE TABLE pegboard_games (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id   uuid NOT NULL REFERENCES pegboard_sessions(id) ON DELETE CASCADE,
    court_id     uuid NOT NULL REFERENCES pegboard_courts(id) ON DELETE CASCADE,
    type         text NOT NULL CHECK (type IN ('Singles', 'Doubles', 'Mixed', 'Funny')),
    status       text NOT NULL DEFAULT 'Active'
                 CHECK (status IN ('Active', 'Finished', 'Cancelled')),
    winner_side  text NULL CHECK (winner_side IN ('A', 'B')),
    score        text NULL,
    started_at   timestamptz NOT NULL DEFAULT now(),
    ended_at     timestamptz NULL,
    CHECK (status <> 'Finished' OR winner_side IS NOT NULL)
);
CREATE INDEX ix_pegboard_games_session ON pegboard_games (session_id);
CREATE INDEX ix_pegboard_games_court ON pegboard_games (court_id);
-- At most one active game per court.
CREATE UNIQUE INDEX ux_pegboard_game_active_per_court
    ON pegboard_games (court_id) WHERE status = 'Active';

-- Which attendances are on which side of a game.
CREATE TABLE pegboard_game_players (
    game_id       uuid NOT NULL REFERENCES pegboard_games(id) ON DELETE CASCADE,
    attendance_id uuid NOT NULL REFERENCES pegboard_attendances(id) ON DELETE RESTRICT,
    side          text NOT NULL CHECK (side IN ('A', 'B')),
    PRIMARY KEY (game_id, attendance_id)
);
CREATE INDEX ix_pegboard_game_players_attendance ON pegboard_game_players (attendance_id);
```

- [ ] **Step 2: Verify the migration applies (it runs on startup)**

Run: `dotnet build`
Then run the migrator via the existing repository integration tests in the next tasks — DbUp applies `0026` on the first test-container spin-up. To check in isolation now:

Run: `dotnet test --filter "FullyQualifiedName~Migrations" 2>&1 | tail -20`
Expected: existing migration tests still PASS (schema applies cleanly). If there is no migration test, this is verified implicitly when Task 6 repository tests run green.

- [ ] **Step 3: Commit**

```bash
git add Migrations/Scripts/0026_create_pegboard.sql
git commit -m "feat(pegboard): schema for sessions, courts, attendances, games + player grade"
```

### Task 2: Enum models

**Files:**
- Create: `Models/PegboardSessionStatus.cs`, `Models/AttendanceStatus.cs`, `Models/GameType.cs`, `Models/GameStatus.cs`, `Models/GameSide.cs`

- [ ] **Step 1: Write the enums**

`Models/PegboardSessionStatus.cs`:
```csharp
namespace smash_dates.Models;

public enum PegboardSessionStatus
{
    Open,
    Closed,
}
```

`Models/AttendanceStatus.cs`:
```csharp
namespace smash_dates.Models;

public enum AttendanceStatus
{
    Waiting,
    Playing,
    Resting,
    Left,
}
```

`Models/GameType.cs`:
```csharp
namespace smash_dates.Models;

public enum GameType
{
    Singles,
    Doubles,
    Mixed,
    Funny,
}
```

`Models/GameStatus.cs`:
```csharp
namespace smash_dates.Models;

public enum GameStatus
{
    Active,
    Finished,
    Cancelled,
}
```

`Models/GameSide.cs`:
```csharp
namespace smash_dates.Models;

public enum GameSide
{
    A,
    B,
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: builds clean.

- [ ] **Step 3: Commit**

```bash
git add Models/PegboardSessionStatus.cs Models/AttendanceStatus.cs Models/GameType.cs Models/GameStatus.cs Models/GameSide.cs
git commit -m "feat(pegboard): enum models"
```

### Task 3: Entity models + Player.Grade

**Files:**
- Create: `Models/SessionHostGrant.cs`, `Models/PegboardSession.cs`, `Models/PegboardCourt.cs`, `Models/PegboardAttendance.cs`, `Models/PegboardGame.cs`, `Models/PegboardGamePlayer.cs`
- Modify: `Models/Player.cs`

- [ ] **Step 1: Write the entity models**

`Models/SessionHostGrant.cs`:
```csharp
namespace smash_dates.Models;

public sealed class SessionHostGrant
{
    public Guid ClubId { get; init; }
    public Guid UserId { get; init; }
    public DateTime GrantedAt { get; init; }
    public Guid? GrantedBy { get; init; }
}
```

`Models/PegboardSession.cs`:
```csharp
namespace smash_dates.Models;

public sealed class PegboardSession
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public PegboardSessionStatus Status { get; init; }
    public Guid? OpenedBy { get; init; }
    public DateTime OpenedAt { get; init; }
    public DateTime? ClosedAt { get; init; }
}
```

`Models/PegboardCourt.cs`:
```csharp
namespace smash_dates.Models;

public sealed class PegboardCourt
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public string Label { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
```

`Models/PegboardAttendance.cs`:
```csharp
namespace smash_dates.Models;

public sealed class PegboardAttendance
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public Guid? PlayerId { get; init; }
    public string? GuestName { get; init; }
    public Gender Gender { get; init; }
    public int? Grade { get; init; }
    public AttendanceStatus Status { get; init; }
    public DateTime WaitingSince { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

`Models/PegboardGame.cs`:
```csharp
namespace smash_dates.Models;

public sealed class PegboardGame
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public Guid CourtId { get; init; }
    public GameType Type { get; init; }
    public GameStatus Status { get; init; }
    public GameSide? WinnerSide { get; init; }
    public string? Score { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
}
```

`Models/PegboardGamePlayer.cs`:
```csharp
namespace smash_dates.Models;

public sealed class PegboardGamePlayer
{
    public Guid GameId { get; init; }
    public Guid AttendanceId { get; init; }
    public GameSide Side { get; init; }
}
```

- [ ] **Step 2: Add Grade to Player**

In `Models/Player.cs`, add the property (the existing `Gender Gender { get; init; }` line is the anchor):

```csharp
    public int? Grade { get; init; }
```

Note `Gender` is the existing enum (`Male | Female`) — reuse it for `PegboardAttendance.Gender`. Confirm `Models/Gender.cs` has `Male, Female`; it does.

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: builds clean.

- [ ] **Step 4: Commit**

```bash
git add Models/SessionHostGrant.cs Models/PegboardSession.cs Models/PegboardCourt.cs Models/PegboardAttendance.cs Models/PegboardGame.cs Models/PegboardGamePlayer.cs Models/Player.cs
git commit -m "feat(pegboard): entity models + Player.Grade"
```

---

## Phase 2 — Repositories, auth helper, grade plumbing

### Task 4: `SessionHostRepository`

**Files:**
- Create: `Repositories/ISessionHostRepository.cs`, `Repositories/SessionHostRepository.cs`
- Test: `tests/smash-dates.IntegrationTests/Repositories/SessionHostRepositoryTests.cs`

- [ ] **Step 1: Write the interface**

`Repositories/ISessionHostRepository.cs`:
```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ISessionHostRepository
{
    Task<bool> IsHostAsync(Guid clubId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionHostGrant>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task GrantAsync(Guid clubId, Guid userId, Guid? grantedBy, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid clubId, Guid userId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write the failing repository test**

`tests/smash-dates.IntegrationTests/Repositories/SessionHostRepositoryTests.cs` (mirror `ClubAdminRepositoryTests`; use the same fixture base — open the repo file to copy its `IntegrationTestBase` ctor + `Seeder` usage):
```csharp
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

public sealed class SessionHostRepositoryTests : IntegrationTestBase
{
    public SessionHostRepositoryTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Grant_ThenIsHost_True_AndRevoke_RemovesIt()
    {
        var user = await Seeder.CreateUserAsync("host@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var repo = new SessionHostRepository(Factory);

        (await repo.IsHostAsync(clubId, user.Id)).Should().BeFalse();
        await repo.GrantAsync(clubId, user.Id, user.Id);
        (await repo.IsHostAsync(clubId, user.Id)).Should().BeTrue();
        (await repo.ListByClubAsync(clubId)).Should().ContainSingle(g => g.UserId == user.Id);
        (await repo.RevokeAsync(clubId, user.Id)).Should().BeTrue();
        (await repo.IsHostAsync(clubId, user.Id)).Should().BeFalse();
    }
}
```
Note: `Factory` is the `IDbConnectionFactory` exposed by `IntegrationTestBase` — confirm its property name in `tests/smash-dates.IntegrationTests/Infrastructure/IntegrationTestBase.cs` and match it (it may be `Factory`, `ConnectionFactory`, or accessed via `Services`). Use whatever existing repository tests use.

- [ ] **Step 3: Run the test, expect failure**

Run: `dotnet test --filter "FullyQualifiedName~SessionHostRepositoryTests" 2>&1 | tail -20`
Expected: FAIL — `SessionHostRepository` does not exist.

- [ ] **Step 4: Write the implementation**

`Repositories/SessionHostRepository.cs` (mirror `ClubAdminRepository` style):
```csharp
using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class SessionHostRepository : ISessionHostRepository
{
    private readonly IDbConnectionFactory _factory;

    public SessionHostRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<bool> IsHostAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM session_hosts WHERE club_id = @clubId AND user_id = @userId)",
                new { clubId, userId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SessionHostGrant>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<SessionHostGrant>(
            new CommandDefinition(
                @"SELECT club_id, user_id, granted_at, granted_by
                  FROM session_hosts WHERE club_id = @clubId ORDER BY granted_at",
                new { clubId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task GrantAsync(Guid clubId, Guid userId, Guid? grantedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO session_hosts (club_id, user_id, granted_by)
                  VALUES (@clubId, @userId, @grantedBy)
                  ON CONFLICT (club_id, user_id) DO NOTHING",
                new { clubId, userId, grantedBy },
                cancellationToken: ct));
    }

    public async Task<bool> RevokeAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM session_hosts WHERE club_id = @clubId AND user_id = @userId",
                new { clubId, userId },
                cancellationToken: ct));
        return rows > 0;
    }
}
```

- [ ] **Step 5: Run the test, expect pass**

Run: `dotnet test --filter "FullyQualifiedName~SessionHostRepositoryTests" 2>&1 | tail -20`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Repositories/ISessionHostRepository.cs Repositories/SessionHostRepository.cs tests/smash-dates.IntegrationTests/Repositories/SessionHostRepositoryTests.cs
git commit -m "feat(pegboard): SessionHostRepository"
```

### Task 5: `SessionAuthorizer`

**Files:**
- Create: `Services/Auth/SessionAuthorizer.cs`

- [ ] **Step 1: Write the authorizer** (mirrors `ClubAuthorizer`, but ClubAdmin OR SessionHost OR SystemAdmin)

`Services/Auth/SessionAuthorizer.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Repositories;

namespace smash_dates.Services.Auth;

/// A request may run a club's pegboard session if the caller is SystemAdmin, a ClubAdmin
/// of the club, or a SessionHost of the club. Returns null on success, else a 401/403 IResult.
public static class SessionAuthorizer
{
    public static async Task<IResult?> RequireSessionRunnerAsync(
        ClaimsPrincipal principal,
        Guid clubId,
        IClubAdminRepository admins,
        ISessionHostRepository hosts,
        CancellationToken ct)
    {
        var userId = principal.UserId();
        if (userId is null) return Results.Unauthorized();
        if (principal.IsSystemAdmin()) return null;
        if (await admins.IsAdminAsync(clubId, userId.Value, ct)) return null;
        if (await hosts.IsHostAsync(clubId, userId.Value, ct)) return null;
        return Results.Forbid();
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: builds clean.

- [ ] **Step 3: Commit**

```bash
git add Services/Auth/SessionAuthorizer.cs
git commit -m "feat(pegboard): SessionAuthorizer (ClubAdmin or SessionHost or SystemAdmin)"
```

### Task 6: Carry Grade through PlayerRepository

**Files:**
- Modify: `Repositories/IPlayerRepository.cs`, `Repositories/PlayerRepository.cs`
- Test: `tests/smash-dates.IntegrationTests/Repositories/PlayerRepositoryTests.cs` (create if absent, else add a fact)

- [ ] **Step 1: Write the failing test**

Add to a `PlayerRepositoryTests` (create the file mirroring another repo test if it doesn't exist):
```csharp
[Fact]
public async Task SetGrade_ThenGetById_ReturnsGrade()
{
    var repo = new PlayerRepository(Factory);
    var id = await repo.CreateAsync("Pat Smith", smash_dates.Models.Gender.Female);
    await repo.SetGradeAsync(id, 2);
    var player = await repo.GetByIdAsync(id);
    player!.Grade.Should().Be(2);
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test --filter "FullyQualifiedName~PlayerRepositoryTests.SetGrade" 2>&1 | tail -20`
Expected: FAIL — `SetGradeAsync` not defined.

- [ ] **Step 3: Implement**

In `Repositories/IPlayerRepository.cs` add to the interface:
```csharp
    Task<bool> SetGradeAsync(Guid playerId, int? grade, CancellationToken ct = default);
```
Add `int? Grade` to the `PlayerClubView` record:
```csharp
public sealed record PlayerClubView(Guid PlayerId, string FullName, Gender Gender, PlayerClubType Type, int? Grade);
```

In `Repositories/PlayerRepository.cs`:
- `GetByIdAsync` SELECT → `"SELECT id, full_name, gender, grade, created_at, updated_at FROM players WHERE id = @id"`.
- `SearchAsync` SELECT → add `grade` to the column list.
- `ListByClubAsync` SELECT → `"SELECT p.id AS player_id, p.full_name, p.gender, pc.type, p.grade FROM player_clubs pc JOIN players p ON p.id = pc.player_id WHERE pc.club_id = @clubId ORDER BY p.full_name"`.
- Add the method:
```csharp
    public async Task<bool> SetGradeAsync(Guid playerId, int? grade, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "UPDATE players SET grade = @grade, updated_at = now() WHERE id = @playerId",
                new { playerId, grade },
                cancellationToken: ct));
        return rows > 0;
    }
```

- [ ] **Step 4: Run, expect pass**

Run: `dotnet test --filter "FullyQualifiedName~PlayerRepositoryTests.SetGrade" 2>&1 | tail -20`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Repositories/IPlayerRepository.cs Repositories/PlayerRepository.cs tests/smash-dates.IntegrationTests/Repositories/PlayerRepositoryTests.cs
git commit -m "feat(pegboard): carry Player.Grade through repository"
```

### Task 7: `PegboardRepository` (the session aggregate)

This repo owns sessions, courts, attendances, games, game_players, the assembled **board read**, and **night stats**. Game start/finish/cancel are transactional (mirror `ClubRepository.CreateWithFirstAdminAsync`'s open-then-`BeginTransaction` pattern).

**Files:**
- Create: `Repositories/IPegboardRepository.cs`, `Repositories/PegboardRepository.cs`
- Test: `tests/smash-dates.IntegrationTests/Repositories/PegboardRepositoryTests.cs`

- [ ] **Step 1: Write the interface + read DTOs**

`Repositories/IPegboardRepository.cs`:
```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

// Read DTOs for the assembled board.
public sealed record BoardGamePlayer(Guid AttendanceId, string DisplayName, Gender Gender, int? Grade, GameSide Side);
public sealed record BoardGame(Guid Id, GameType Type, IReadOnlyList<BoardGamePlayer> Players);
public sealed record BoardCourt(Guid Id, string Label, BoardGame? ActiveGame);
public sealed record BoardAttendee(
    Guid Id, Guid? PlayerId, string DisplayName, Gender Gender, int? Grade,
    AttendanceStatus Status, DateTime WaitingSince,
    int GamesPlayed, int GamesWon);
public sealed record BoardView(
    PegboardSession Session,
    IReadOnlyList<BoardCourt> Courts,
    IReadOnlyList<BoardAttendee> Attendees);

// One attendee's makeup-relevant facts, used by the fill engine.
public sealed record WaitingAttendee(Guid Id, Gender Gender, int? Grade, DateTime WaitingSince, int GamesPlayed);

public interface IPegboardRepository
{
    // Sessions
    Task<PegboardSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<PegboardSession?> GetOpenByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<IReadOnlyList<PegboardSession>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task<Guid> OpenAsync(Guid clubId, string name, Guid openedBy, CancellationToken ct = default);
    Task<bool> CloseAsync(Guid sessionId, CancellationToken ct = default);

    // Courts
    Task<PegboardCourt?> GetCourtAsync(Guid courtId, CancellationToken ct = default);
    Task<Guid> AddCourtAsync(Guid sessionId, string label, CancellationToken ct = default);
    Task<bool> HasActiveGameOnCourtAsync(Guid courtId, CancellationToken ct = default);
    Task<bool> RemoveCourtAsync(Guid courtId, CancellationToken ct = default);

    // Attendances
    Task<PegboardAttendance?> GetAttendanceAsync(Guid attendanceId, CancellationToken ct = default);
    Task<Guid> AddPlayerAttendanceAsync(Guid sessionId, Guid playerId, Gender gender, int? grade, CancellationToken ct = default);
    Task<Guid> AddGuestAttendanceAsync(Guid sessionId, string guestName, Gender gender, int? grade, CancellationToken ct = default);
    Task<bool> SetAttendanceStatusAsync(Guid attendanceId, AttendanceStatus status, CancellationToken ct = default);
    Task<bool> IsInActiveGameAsync(Guid attendanceId, CancellationToken ct = default);
    Task<bool> RemoveAttendanceAsync(Guid attendanceId, CancellationToken ct = default);
    Task<IReadOnlyList<WaitingAttendee>> ListWaitingAsync(Guid sessionId, CancellationToken ct = default);

    // Games
    Task<PegboardGame?> GetGameAsync(Guid gameId, CancellationToken ct = default);
    Task<Guid> StartGameAsync(Guid sessionId, Guid courtId, GameType type,
        IReadOnlyList<Guid> sideA, IReadOnlyList<Guid> sideB, CancellationToken ct = default);
    Task<bool> FinishGameAsync(Guid gameId, GameSide winnerSide, string? score, CancellationToken ct = default);
    Task<bool> CancelGameAsync(Guid gameId, CancellationToken ct = default);

    // Read model
    Task<BoardView?> GetBoardAsync(Guid sessionId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write the failing repository test** (covers the spine: open → court → attendances → start → finish → board/stats)

`tests/smash-dates.IntegrationTests/Repositories/PegboardRepositoryTests.cs`:
```csharp
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

public sealed class PegboardRepositoryTests : IntegrationTestBase
{
    public PegboardRepositoryTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task FullSpine_Open_Court_Attend_Play_Finish_UpdatesBoardAndStats()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var opener = await Seeder.CreateUserAsync("h@example.com", "correct-horse-battery");
        var repo = new PegboardRepository(Factory);

        var sessionId = await repo.OpenAsync(clubId, "Tuesday Club Night", opener.Id);
        var courtId = await repo.AddCourtAsync(sessionId, "Court 1");

        var a1 = await repo.AddGuestAttendanceAsync(sessionId, "Alice", Gender.Female, 2);
        var a2 = await repo.AddGuestAttendanceAsync(sessionId, "Bob", Gender.Male, 3);

        var gameId = await repo.StartGameAsync(sessionId, courtId, GameType.Singles, [a1], [a2]);

        var mid = await repo.GetBoardAsync(sessionId);
        mid!.Courts.Single().ActiveGame!.Players.Should().HaveCount(2);
        mid.Attendees.Should().OnlyContain(x => x.Status == AttendanceStatus.Playing);

        (await repo.FinishGameAsync(gameId, GameSide.A, "21-15")).Should().BeTrue();

        var after = await repo.GetBoardAsync(sessionId);
        after!.Courts.Single().ActiveGame.Should().BeNull();
        after.Attendees.Should().OnlyContain(x => x.Status == AttendanceStatus.Waiting);
        after.Attendees.Single(x => x.Id == a1).GamesWon.Should().Be(1);
        after.Attendees.Single(x => x.Id == a2).GamesWon.Should().Be(0);
        after.Attendees.Should().OnlyContain(x => x.GamesPlayed == 1);
    }

    [Fact]
    public async Task Open_SecondOpenSession_SameClub_Throws()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var u = await Seeder.CreateUserAsync("h2@example.com", "correct-horse-battery");
        var repo = new PegboardRepository(Factory);
        await repo.OpenAsync(clubId, "First", u.Id);

        var act = async () => await repo.OpenAsync(clubId, "Second", u.Id);
        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
```

- [ ] **Step 3: Run, expect failure**

Run: `dotnet test --filter "FullyQualifiedName~PegboardRepositoryTests" 2>&1 | tail -20`
Expected: FAIL — `PegboardRepository` not defined.

- [ ] **Step 4: Implement the repository**

`Repositories/PegboardRepository.cs`:
```csharp
using System.Data;
using Dapper;
using Npgsql;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class PegboardRepository : IPegboardRepository
{
    private const string SessionCols = "id, club_id, name, status, opened_by, opened_at, closed_at";
    private readonly IDbConnectionFactory _factory;

    public PegboardRepository(IDbConnectionFactory factory) => _factory = factory;

    // ---- Sessions ----
    public async Task<PegboardSession?> GetSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardSession>(new CommandDefinition(
            $"SELECT {SessionCols} FROM pegboard_sessions WHERE id = @sessionId", new { sessionId }, cancellationToken: ct));
    }

    public async Task<PegboardSession?> GetOpenByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardSession>(new CommandDefinition(
            $"SELECT {SessionCols} FROM pegboard_sessions WHERE club_id = @clubId AND status = 'Open'",
            new { clubId }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PegboardSession>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<PegboardSession>(new CommandDefinition(
            $"SELECT {SessionCols} FROM pegboard_sessions WHERE club_id = @clubId ORDER BY opened_at DESC",
            new { clubId }, cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> OpenAsync(Guid clubId, string name, Guid openedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_sessions (club_id, name, opened_by)
              VALUES (@clubId, @name, @openedBy) RETURNING id",
            new { clubId, name, openedBy }, cancellationToken: ct));
    }

    public async Task<bool> CloseAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await OpenConnAsync(conn, ct);
        using var tx = conn.BeginTransaction();
        // End any in-progress games with no result (cancelled), then close the session.
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_games SET status = 'Cancelled', ended_at = now()
              WHERE session_id = @sessionId AND status = 'Active'",
            new { sessionId }, transaction: tx, cancellationToken: ct));
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_sessions SET status = 'Closed', closed_at = now()
              WHERE id = @sessionId AND status = 'Open'",
            new { sessionId }, transaction: tx, cancellationToken: ct));
        tx.Commit();
        return rows > 0;
    }

    // ---- Courts ----
    public async Task<PegboardCourt?> GetCourtAsync(Guid courtId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardCourt>(new CommandDefinition(
            "SELECT id, session_id, label, created_at FROM pegboard_courts WHERE id = @courtId",
            new { courtId }, cancellationToken: ct));
    }

    public async Task<Guid> AddCourtAsync(Guid sessionId, string label, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "INSERT INTO pegboard_courts (session_id, label) VALUES (@sessionId, @label) RETURNING id",
            new { sessionId, label }, cancellationToken: ct));
    }

    public async Task<bool> HasActiveGameOnCourtAsync(Guid courtId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM pegboard_games WHERE court_id = @courtId AND status = 'Active')",
            new { courtId }, cancellationToken: ct));
    }

    public async Task<bool> RemoveCourtAsync(Guid courtId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM pegboard_courts WHERE id = @courtId", new { courtId }, cancellationToken: ct));
        return rows > 0;
    }

    // ---- Attendances ----
    public async Task<PegboardAttendance?> GetAttendanceAsync(Guid attendanceId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardAttendance>(new CommandDefinition(
            @"SELECT id, session_id, player_id, guest_name, gender, grade, status, waiting_since, created_at
              FROM pegboard_attendances WHERE id = @attendanceId", new { attendanceId }, cancellationToken: ct));
    }

    public async Task<Guid> AddPlayerAttendanceAsync(Guid sessionId, Guid playerId, Gender gender, int? grade, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_attendances (session_id, player_id, gender, grade)
              VALUES (@sessionId, @playerId, @gender, @grade) RETURNING id",
            new { sessionId, playerId, gender = gender.ToString(), grade }, cancellationToken: ct));
    }

    public async Task<Guid> AddGuestAttendanceAsync(Guid sessionId, string guestName, Gender gender, int? grade, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_attendances (session_id, guest_name, gender, grade)
              VALUES (@sessionId, @guestName, @gender, @grade) RETURNING id",
            new { sessionId, guestName, gender = gender.ToString(), grade }, cancellationToken: ct));
    }

    public async Task<bool> SetAttendanceStatusAsync(Guid attendanceId, AttendanceStatus status, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        // Returning to the queue refreshes wait time so finished/rejoining players go to the tail.
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_attendances
              SET status = @status,
                  waiting_since = CASE WHEN @status = 'Waiting' THEN now() ELSE waiting_since END
              WHERE id = @attendanceId",
            new { attendanceId, status = status.ToString() }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> IsInActiveGameAsync(Guid attendanceId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT EXISTS (
                SELECT 1 FROM pegboard_game_players gp
                JOIN pegboard_games g ON g.id = gp.game_id
                WHERE gp.attendance_id = @attendanceId AND g.status = 'Active')",
            new { attendanceId }, cancellationToken: ct));
    }

    public async Task<bool> RemoveAttendanceAsync(Guid attendanceId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM pegboard_attendances WHERE id = @attendanceId", new { attendanceId }, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<IReadOnlyList<WaitingAttendee>> ListWaitingAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<WaitingAttendee>(new CommandDefinition(
            @"SELECT a.id, a.gender, a.grade, a.waiting_since,
                     (SELECT count(*) FROM pegboard_game_players gp
                      JOIN pegboard_games g ON g.id = gp.game_id
                      WHERE gp.attendance_id = a.id AND g.status = 'Finished') AS games_played
              FROM pegboard_attendances a
              WHERE a.session_id = @sessionId AND a.status = 'Waiting'
              ORDER BY a.waiting_since",
            new { sessionId }, cancellationToken: ct));
        return rows.AsList();
    }

    // ---- Games ----
    public async Task<PegboardGame?> GetGameAsync(Guid gameId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<PegboardGame>(new CommandDefinition(
            @"SELECT id, session_id, court_id, type, status, winner_side, score, started_at, ended_at
              FROM pegboard_games WHERE id = @gameId", new { gameId }, cancellationToken: ct));
    }

    public async Task<Guid> StartGameAsync(Guid sessionId, Guid courtId, GameType type,
        IReadOnlyList<Guid> sideA, IReadOnlyList<Guid> sideB, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await OpenConnAsync(conn, ct);
        using var tx = conn.BeginTransaction();

        var gameId = await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(
            @"INSERT INTO pegboard_games (session_id, court_id, type)
              VALUES (@sessionId, @courtId, @type) RETURNING id",
            new { sessionId, courtId, type = type.ToString() }, transaction: tx, cancellationToken: ct));

        foreach (var id in sideA)
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO pegboard_game_players (game_id, attendance_id, side) VALUES (@gameId, @id, 'A')",
                new { gameId, id }, transaction: tx, cancellationToken: ct));
        foreach (var id in sideB)
            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO pegboard_game_players (game_id, attendance_id, side) VALUES (@gameId, @id, 'B')",
                new { gameId, id }, transaction: tx, cancellationToken: ct));

        var all = sideA.Concat(sideB).ToArray();
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE pegboard_attendances SET status = 'Playing' WHERE id = ANY(@all)",
            new { all }, transaction: tx, cancellationToken: ct));

        tx.Commit();
        return gameId;
    }

    public async Task<bool> FinishGameAsync(Guid gameId, GameSide winnerSide, string? score, CancellationToken ct = default)
        => await EndGameAsync(gameId, "Finished", winnerSide.ToString(), score, ct);

    public async Task<bool> CancelGameAsync(Guid gameId, CancellationToken ct = default)
        => await EndGameAsync(gameId, "Cancelled", null, null, ct);

    private async Task<bool> EndGameAsync(Guid gameId, string status, string? winnerSide, string? score, CancellationToken ct)
    {
        using var conn = _factory.Create();
        await OpenConnAsync(conn, ct);
        using var tx = conn.BeginTransaction();

        var rows = await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_games
              SET status = @status, winner_side = @winnerSide, score = @score, ended_at = now()
              WHERE id = @gameId AND status = 'Active'",
            new { gameId, status, winnerSide, score }, transaction: tx, cancellationToken: ct));
        if (rows == 0) { tx.Rollback(); return false; }

        // Return the game's players to the queue tail.
        await conn.ExecuteAsync(new CommandDefinition(
            @"UPDATE pegboard_attendances SET status = 'Waiting', waiting_since = now()
              WHERE id IN (SELECT attendance_id FROM pegboard_game_players WHERE game_id = @gameId)
                AND status = 'Playing'",
            new { gameId }, transaction: tx, cancellationToken: ct));

        tx.Commit();
        return true;
    }

    // ---- Board read ----
    public async Task<BoardView?> GetBoardAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await GetSessionAsync(sessionId, ct);
        if (session is null) return null;

        using var conn = _factory.Create();

        var courts = (await conn.QueryAsync<(Guid Id, string Label)>(new CommandDefinition(
            "SELECT id, label FROM pegboard_courts WHERE session_id = @sessionId ORDER BY created_at",
            new { sessionId }, cancellationToken: ct))).AsList();

        var activeGames = (await conn.QueryAsync<(Guid Id, Guid CourtId, string Type)>(new CommandDefinition(
            "SELECT id, court_id, type FROM pegboard_games WHERE session_id = @sessionId AND status = 'Active'",
            new { sessionId }, cancellationToken: ct))).AsList();

        var gamePlayers = (await conn.QueryAsync<(Guid GameId, Guid AttendanceId, string Side, string DisplayName, string Gender, int? Grade)>(
            new CommandDefinition(
            @"SELECT gp.game_id, gp.attendance_id, gp.side,
                     COALESCE(a.guest_name, p.full_name) AS display_name, a.gender, a.grade
              FROM pegboard_game_players gp
              JOIN pegboard_games g ON g.id = gp.game_id
              JOIN pegboard_attendances a ON a.id = gp.attendance_id
              LEFT JOIN players p ON p.id = a.player_id
              WHERE g.session_id = @sessionId AND g.status = 'Active'",
            new { sessionId }, cancellationToken: ct))).AsList();

        var attendees = (await conn.QueryAsync<(Guid Id, Guid? PlayerId, string DisplayName, string Gender, int? Grade, string Status, DateTime WaitingSince, int GamesPlayed, int GamesWon)>(
            new CommandDefinition(
            @"SELECT a.id, a.player_id, COALESCE(a.guest_name, p.full_name) AS display_name,
                     a.gender, a.grade, a.status, a.waiting_since,
                     (SELECT count(*) FROM pegboard_game_players gp
                      JOIN pegboard_games g ON g.id = gp.game_id
                      WHERE gp.attendance_id = a.id AND g.status = 'Finished') AS games_played,
                     (SELECT count(*) FROM pegboard_game_players gp
                      JOIN pegboard_games g ON g.id = gp.game_id
                      WHERE gp.attendance_id = a.id AND g.status = 'Finished'
                        AND g.winner_side = gp.side) AS games_won
              FROM pegboard_attendances a
              LEFT JOIN players p ON p.id = a.player_id
              WHERE a.session_id = @sessionId
              ORDER BY a.waiting_since",
            new { sessionId }, cancellationToken: ct))).AsList();

        BoardGame? GameForCourt(Guid courtId)
        {
            var g = activeGames.FirstOrDefault(x => x.CourtId == courtId);
            if (g.Id == Guid.Empty) return null;
            var players = gamePlayers.Where(p => p.GameId == g.Id)
                .Select(p => new BoardGamePlayer(p.AttendanceId, p.DisplayName,
                    Enum.Parse<Gender>(p.Gender), p.Grade, Enum.Parse<GameSide>(p.Side)))
                .ToList();
            return new BoardGame(g.Id, Enum.Parse<GameType>(g.Type), players);
        }

        var boardCourts = courts.Select(c => new BoardCourt(c.Id, c.Label, GameForCourt(c.Id))).ToList();
        var boardAttendees = attendees.Select(a => new BoardAttendee(
            a.Id, a.PlayerId, a.DisplayName, Enum.Parse<Gender>(a.Gender), a.Grade,
            Enum.Parse<AttendanceStatus>(a.Status), a.WaitingSince, a.GamesPlayed, a.GamesWon)).ToList();

        return new BoardView(session, boardCourts, boardAttendees);
    }

    private static async Task OpenConnAsync(IDbConnection conn, CancellationToken ct)
    {
        if (conn is System.Data.Common.DbConnection db) await db.OpenAsync(ct);
        else conn.Open();
    }
}
```

> Note: Dapper maps the `(...)` value-tuple columns positionally by the `AS` aliases; if the test runner's Dapper version rejects positional tuple mapping, switch those queries to small private `record` row types. The codebase already maps snake_case → PascalCase via Dapper's default matching, so prefer named row records if in doubt.

- [ ] **Step 5: Run, expect pass**

Run: `dotnet test --filter "FullyQualifiedName~PegboardRepositoryTests" 2>&1 | tail -30`
Expected: PASS (both facts).

- [ ] **Step 6: Commit**

```bash
git add Repositories/IPegboardRepository.cs Repositories/PegboardRepository.cs tests/smash-dates.IntegrationTests/Repositories/PegboardRepositoryTests.cs
git commit -m "feat(pegboard): PegboardRepository aggregate + board read + night stats"
```

---

## Phase 3 — Services (makeup, fill engine, SSE pub/sub)

### Task 8: `GameMakeup` (pure validation helper)

Used by start-game (to produce a warning) and the filler (to form valid lineups). Pure, no DB → unit-tested.

**Files:**
- Create: `Services/Pegboard/GameMakeup.cs`
- Test: `tests/smash-dates.UnitTests/Services/Pegboard/GameMakeupTests.cs`

- [ ] **Step 1: Write the failing unit test**

`tests/smash-dates.UnitTests/Services/Pegboard/GameMakeupTests.cs`:
```csharp
using smash_dates.Models;
using smash_dates.Services.Pegboard;

namespace smash_dates.UnitTests.Services.Pegboard;

public class GameMakeupTests
{
    [Fact]
    public void Singles_NeedsOnePerSide()
        => GameMakeup.SideSize(GameType.Singles).Should().Be(1);

    [Theory]
    [InlineData(GameType.Doubles)]
    [InlineData(GameType.Mixed)]
    [InlineData(GameType.Funny)]
    public void FourPlayerTypes_NeedTwoPerSide(GameType type)
        => GameMakeup.SideSize(type).Should().Be(2);

    [Fact]
    public void Doubles_AllSameGender_IsValid()
        => GameMakeup.IsValid(GameType.Doubles,
            [Gender.Male, Gender.Male], [Gender.Male, Gender.Male]).Should().BeTrue();

    [Fact]
    public void Doubles_MixedGenders_IsInvalid()
        => GameMakeup.IsValid(GameType.Doubles,
            [Gender.Male, Gender.Female], [Gender.Male, Gender.Male]).Should().BeFalse();

    [Fact]
    public void Mixed_EachSideOneMaleOneFemale_IsValid()
        => GameMakeup.IsValid(GameType.Mixed,
            [Gender.Male, Gender.Female], [Gender.Male, Gender.Female]).Should().BeTrue();

    [Fact]
    public void Mixed_SideOfTwoMales_IsInvalid()
        => GameMakeup.IsValid(GameType.Mixed,
            [Gender.Male, Gender.Male], [Gender.Female, Gender.Female]).Should().BeFalse();

    [Fact]
    public void Funny_AnyFourPlayerArrangement_IsValid()
        => GameMakeup.IsValid(GameType.Funny,
            [Gender.Male, Gender.Male], [Gender.Female, Gender.Female]).Should().BeTrue();

    [Fact]
    public void WrongCount_IsInvalid()
        => GameMakeup.IsValid(GameType.Singles,
            [Gender.Male, Gender.Male], [Gender.Female]).Should().BeFalse();
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/smash-dates.UnitTests --filter "FullyQualifiedName~GameMakeupTests" 2>&1 | tail -20`
Expected: FAIL — `GameMakeup` not defined.

- [ ] **Step 3: Implement**

`Services/Pegboard/GameMakeup.cs`:
```csharp
using smash_dates.Models;

namespace smash_dates.Services.Pegboard;

/// Per-side player counts and gender-makeup rules per game type. Funny is the
/// catch-all 4-player type and is always considered a valid makeup.
public static class GameMakeup
{
    public static int SideSize(GameType type) => type == GameType.Singles ? 1 : 2;

    public static bool IsValid(GameType type, IReadOnlyList<Gender> sideA, IReadOnlyList<Gender> sideB)
    {
        var size = SideSize(type);
        if (sideA.Count != size || sideB.Count != size) return false;

        return type switch
        {
            GameType.Singles => true,
            GameType.Funny => true,
            GameType.Doubles => sideA.Concat(sideB).Distinct().Count() == 1, // all four one gender
            GameType.Mixed => OneEach(sideA) && OneEach(sideB),
            _ => false,
        };
    }

    private static bool OneEach(IReadOnlyList<Gender> side)
        => side.Count == 2 && side[0] != side[1];
}
```

- [ ] **Step 4: Run, expect pass**

Run: `dotnet test tests/smash-dates.UnitTests --filter "FullyQualifiedName~GameMakeupTests" 2>&1 | tail -20`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/Pegboard/GameMakeup.cs tests/smash-dates.UnitTests/Services/Pegboard/GameMakeupTests.cs
git commit -m "feat(pegboard): GameMakeup validation helper"
```

### Task 9: `PegboardFiller` (suggest / auto-fill engine) + played-pairs query

The engine picks a valid lineup from the waiting queue, optimising (in priority): fairness (wait time / fewest games — the input is pre-ordered), valid makeup for the type, partner/opponent variety (avoid pairs already seen tonight), and ability balance (even side grade sums; null grade = 3). It scores candidate lineups drawn from a bounded pool (first 8 waiting) to keep combinatorics small.

**Files:**
- Create: `Services/Pegboard/IPegboardFiller.cs`, `Services/Pegboard/PegboardFiller.cs`
- Modify: `Repositories/IPegboardRepository.cs`, `Repositories/PegboardRepository.cs` (add `ListPlayedPairsAsync`)
- Test: `tests/smash-dates.UnitTests/Services/Pegboard/PegboardFillerTests.cs`

- [ ] **Step 1: Add the played-pairs repo method (interface + impl + a fact in `PegboardRepositoryTests`)**

In `IPegboardRepository`:
```csharp
    // Unordered attendance-id pairs that have shared a finished game this session (for variety).
    Task<IReadOnlyList<(Guid A, Guid B)>> ListPlayedPairsAsync(Guid sessionId, CancellationToken ct = default);
```
In `PegboardRepository`:
```csharp
    public async Task<IReadOnlyList<(Guid A, Guid B)>> ListPlayedPairsAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<(Guid A, Guid B)>(new CommandDefinition(
            @"SELECT gp1.attendance_id AS a, gp2.attendance_id AS b
              FROM pegboard_game_players gp1
              JOIN pegboard_game_players gp2 ON gp1.game_id = gp2.game_id AND gp1.attendance_id < gp2.attendance_id
              JOIN pegboard_games g ON g.id = gp1.game_id
              WHERE g.session_id = @sessionId AND g.status = 'Finished'",
            new { sessionId }, cancellationToken: ct));
        return rows.AsList();
    }
```

- [ ] **Step 2: Write the failing unit test for the filler**

`tests/smash-dates.UnitTests/Services/Pegboard/PegboardFillerTests.cs`:
```csharp
using smash_dates.Models;
using smash_dates.Services.Pegboard;

namespace smash_dates.UnitTests.Services.Pegboard;

public class PegboardFillerTests
{
    private static FillCandidate C(string id, Gender g, int? grade, int order)
        => new(Guid.Parse(id.PadLeft(32, '0').Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-")),
               g, grade, order);

    [Fact]
    public void Mixed_FormsOneMaleOneFemalePerSide()
    {
        var pool = new[]
        {
            C("1", Gender.Male, 3, 0),
            C("2", Gender.Female, 3, 1),
            C("3", Gender.Male, 3, 2),
            C("4", Gender.Female, 3, 3),
        };
        var result = PegboardFiller.Suggest(GameType.Mixed, pool, playedPairs: []);
        result.Should().NotBeNull();
        result!.SideA.Should().HaveCount(2);
        result.SideB.Should().HaveCount(2);
        // each side must be 1M+1F
        SideGenders(result.SideA, pool).Should().BeEquivalentTo([Gender.Male, Gender.Female]);
        SideGenders(result.SideB, pool).Should().BeEquivalentTo([Gender.Male, Gender.Female]);
    }

    [Fact]
    public void Mixed_NotEnoughOfAGender_ReturnsNull()
    {
        var pool = new[]
        {
            C("1", Gender.Male, 3, 0),
            C("2", Gender.Male, 3, 1),
            C("3", Gender.Male, 3, 2),
            C("4", Gender.Female, 3, 3),
        };
        PegboardFiller.Suggest(GameType.Mixed, pool, playedPairs: []).Should().BeNull();
    }

    [Fact]
    public void Singles_PrefersLongestWaiting()
    {
        var pool = new[]
        {
            C("1", Gender.Male, 3, 0),
            C("2", Gender.Male, 3, 1),
            C("3", Gender.Male, 3, 2),
        };
        var result = PegboardFiller.Suggest(GameType.Singles, pool, playedPairs: []);
        var chosen = result!.SideA.Concat(result.SideB).ToHashSet();
        chosen.Should().Contain(pool[0].Id);
        chosen.Should().Contain(pool[1].Id);
    }

    private static List<Gender> SideGenders(IReadOnlyList<Guid> side, FillCandidate[] pool)
        => side.Select(id => pool.Single(p => p.Id == id).Gender).ToList();
}
```

- [ ] **Step 3: Run, expect failure**

Run: `dotnet test tests/smash-dates.UnitTests --filter "FullyQualifiedName~PegboardFillerTests" 2>&1 | tail -20`
Expected: FAIL — `PegboardFiller` / `FillCandidate` not defined.

- [ ] **Step 4: Implement the filler**

`Services/Pegboard/IPegboardFiller.cs`:
```csharp
using smash_dates.Models;

namespace smash_dates.Services.Pegboard;

// A waiting attendee considered for a lineup. Order = position in the wait queue (0 = longest waiting).
public sealed record FillCandidate(Guid Id, Gender Gender, int? Grade, int Order);

// A proposed lineup split into two sides.
public sealed record FillSuggestion(IReadOnlyList<Guid> SideA, IReadOnlyList<Guid> SideB);
```

`Services/Pegboard/PegboardFiller.cs`:
```csharp
using smash_dates.Models;

namespace smash_dates.Services.Pegboard;

/// Picks a valid lineup from the waiting queue for a game type, optimising fairness,
/// makeup, partner/opponent variety and grade balance. Pure and deterministic.
public static class PegboardFiller
{
    private const int PoolCap = 8;     // bound combinatorics: only consider the front of the queue
    private const int DefaultGrade = 3;

    public static FillSuggestion? Suggest(
        GameType type, IReadOnlyList<FillCandidate> waiting, IReadOnlyList<(Guid A, Guid B)> playedPairs)
    {
        var size = GameMakeup.SideSize(type);
        var need = size * 2;
        if (waiting.Count < need) return null;

        var pool = waiting.OrderBy(c => c.Order).Take(PoolCap).ToList();
        var pairSet = new HashSet<(Guid, Guid)>();
        foreach (var (a, b) in playedPairs) pairSet.Add(Key(a, b));

        FillSuggestion? best = null;
        double bestScore = double.MaxValue;

        // Enumerate combinations of `need` from the pool, then the best side split.
        foreach (var combo in Combinations(pool, need))
        {
            foreach (var (sideA, sideB) in SideSplits(combo, size))
            {
                var gendersA = sideA.Select(c => c.Gender).ToList();
                var gendersB = sideB.Select(c => c.Gender).ToList();
                if (!GameMakeup.IsValid(type, gendersA, gendersB)) continue;

                var score = Score(sideA, sideB, pairSet);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = new FillSuggestion(sideA.Select(c => c.Id).ToList(), sideB.Select(c => c.Id).ToList());
                }
            }
        }
        return best;
    }

    private static double Score(
        IReadOnlyList<FillCandidate> a, IReadOnlyList<FillCandidate> b, HashSet<(Guid, Guid)> playedPairs)
    {
        // Fairness: prefer players nearer the front of the queue (lower Order sum).
        var fairness = a.Concat(b).Sum(c => c.Order);

        // Variety: penalise every pair (same side or opposite) that already played together tonight.
        var all = a.Concat(b).ToList();
        var repeats = 0;
        for (var i = 0; i < all.Count; i++)
            for (var j = i + 1; j < all.Count; j++)
                if (playedPairs.Contains(Key(all[i].Id, all[j].Id))) repeats++;

        // Grade balance: even the two sides' grade sums (null = mid).
        var gradeImbalance = Math.Abs(a.Sum(c => c.Grade ?? DefaultGrade) - b.Sum(c => c.Grade ?? DefaultGrade));

        // Weights: fairness dominates, then variety, then grade balance.
        return fairness + repeats * 100 + gradeImbalance * 5;
    }

    private static (Guid, Guid) Key(Guid x, Guid y) => x.CompareTo(y) < 0 ? (x, y) : (y, x);

    private static IEnumerable<List<FillCandidate>> Combinations(IReadOnlyList<FillCandidate> items, int k)
    {
        var n = items.Count;
        var idx = Enumerable.Range(0, k).ToArray();
        while (true)
        {
            yield return idx.Select(i => items[i]).ToList();
            var p = k - 1;
            while (p >= 0 && idx[p] == n - k + p) p--;
            if (p < 0) yield break;
            idx[p]++;
            for (var i = p + 1; i < k; i++) idx[i] = idx[i - 1] + 1;
        }
    }

    // Split `combo` of size 2*sideSize into (A,B) of sideSize each. To avoid mirror duplicates,
    // fix the first element on side A.
    private static IEnumerable<(List<FillCandidate> A, List<FillCandidate> B)> SideSplits(
        List<FillCandidate> combo, int sideSize)
    {
        if (sideSize == 1)
        {
            yield return ([combo[0]], [combo[1]]);
            yield break;
        }
        var first = combo[0];
        var rest = combo.Skip(1).ToList();
        // choose (sideSize-1) partners for `first`; the remainder is side B
        foreach (var partnerIdx in Combinations(Enumerable.Range(0, rest.Count).Select(i => new FillCandidate(rest[i].Id, rest[i].Gender, rest[i].Grade, i)).ToList(), sideSize - 1))
        {
            var partnerOrders = partnerIdx.Select(c => c.Order).ToHashSet();
            var a = new List<FillCandidate> { first };
            var b = new List<FillCandidate>();
            for (var i = 0; i < rest.Count; i++)
                (partnerOrders.Contains(i) ? a : b).Add(rest[i]);
            yield return (a, b);
        }
    }
}
```

- [ ] **Step 5: Run, expect pass**

Run: `dotnet test tests/smash-dates.UnitTests --filter "FullyQualifiedName~PegboardFillerTests" 2>&1 | tail -30`
Expected: PASS. Also run the new repo fact: `dotnet test --filter "FullyQualifiedName~PegboardRepositoryTests" 2>&1 | tail -10` → PASS.

- [ ] **Step 6: Commit**

```bash
git add Services/Pegboard/IPegboardFiller.cs Services/Pegboard/PegboardFiller.cs Repositories/IPegboardRepository.cs Repositories/PegboardRepository.cs tests/smash-dates.UnitTests/Services/Pegboard/PegboardFillerTests.cs
git commit -m "feat(pegboard): fill engine (fairness/makeup/variety/grade) + played-pairs query"
```

### Task 10: `PegboardEventPublisher` (in-process SSE pub/sub)

A singleton that lets viewers subscribe to a session's "board changed" signal. Mutation endpoints call `Publish(sessionId)`; the SSE endpoint awaits the next signal. See ADR 0004 — in-process, single-instance.

**Files:**
- Create: `Services/Pegboard/IPegboardEventPublisher.cs`, `Services/Pegboard/PegboardEventPublisher.cs`
- Test: `tests/smash-dates.UnitTests/Services/Pegboard/PegboardEventPublisherTests.cs`

- [ ] **Step 1: Write the failing unit test**

`tests/smash-dates.UnitTests/Services/Pegboard/PegboardEventPublisherTests.cs`:
```csharp
using smash_dates.Services.Pegboard;

namespace smash_dates.UnitTests.Services.Pegboard;

public class PegboardEventPublisherTests
{
    [Fact]
    public async Task Subscriber_ReceivesSignal_ForItsSession()
    {
        var pub = new PegboardEventPublisher();
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var reader = pub.Subscribe(sessionId);
        pub.Publish(sessionId);

        var got = await reader.WaitToReadAsync(cts.Token);
        got.Should().BeTrue();
    }

    [Fact]
    public async Task Subscriber_DoesNotReceive_OtherSessionsSignal()
    {
        var pub = new PegboardEventPublisher();
        var mine = Guid.NewGuid();
        var other = Guid.NewGuid();
        var reader = pub.Subscribe(mine);

        pub.Publish(other);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var act = async () => await reader.WaitToReadAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
```

- [ ] **Step 2: Run, expect failure**

Run: `dotnet test tests/smash-dates.UnitTests --filter "FullyQualifiedName~PegboardEventPublisherTests" 2>&1 | tail -20`
Expected: FAIL — type not defined.

- [ ] **Step 3: Implement**

`Services/Pegboard/IPegboardEventPublisher.cs`:
```csharp
using System.Threading.Channels;

namespace smash_dates.Services.Pegboard;

public interface IPegboardEventPublisher
{
    // Subscribe to a session's board-changed signals. Dispose-free: the reader completes
    // when the caller stops reading (the SSE request ends).
    ChannelReader<byte> Subscribe(Guid sessionId);
    void Unsubscribe(Guid sessionId, ChannelReader<byte> reader);
    void Publish(Guid sessionId);
}
```

`Services/Pegboard/PegboardEventPublisher.cs`:
```csharp
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace smash_dates.Services.Pegboard;

/// In-process fan-out of content-free "board changed" signals, keyed by session.
/// Single-instance only (see docs/adr/0004). Each subscriber gets a bounded channel
/// that drops to the latest signal — a missed tick self-heals on the next read.
public sealed class PegboardEventPublisher : IPegboardEventPublisher
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Channel<byte>, byte>> _subs = new();

    public ChannelReader<byte> Subscribe(Guid sessionId)
    {
        var channel = Channel.CreateBounded<byte>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        var set = _subs.GetOrAdd(sessionId, _ => new ConcurrentDictionary<Channel<byte>, byte>());
        set[channel] = 0;
        return channel.Reader;
    }

    public void Unsubscribe(Guid sessionId, ChannelReader<byte> reader)
    {
        if (!_subs.TryGetValue(sessionId, out var set)) return;
        foreach (var ch in set.Keys)
        {
            if (ReferenceEquals(ch.Reader, reader))
            {
                set.TryRemove(ch, out _);
                ch.Writer.TryComplete();
                break;
            }
        }
        if (set.IsEmpty) _subs.TryRemove(sessionId, out _);
    }

    public void Publish(Guid sessionId)
    {
        if (!_subs.TryGetValue(sessionId, out var set)) return;
        foreach (var ch in set.Keys) ch.Writer.TryWrite(0);
    }
}
```

- [ ] **Step 4: Run, expect pass**

Run: `dotnet test tests/smash-dates.UnitTests --filter "FullyQualifiedName~PegboardEventPublisherTests" 2>&1 | tail -20`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Services/Pegboard/IPegboardEventPublisher.cs Services/Pegboard/PegboardEventPublisher.cs tests/smash-dates.UnitTests/Services/Pegboard/PegboardEventPublisherTests.cs
git commit -m "feat(pegboard): in-process SSE event publisher"
```

---

## Phase 4 — Endpoints & wiring

### Task 11: DI registration + endpoint group wiring in `Program.cs`

Do this first so endpoint files compile as they're added (the `Map...` calls are added per group below; register services now).

**Files:**
- Modify: `Program.cs`

- [ ] **Step 1: Register services** — after the line `builder.Services.AddScoped<ITeamPlayerRepository, TeamPlayerRepository>();`, add:

```csharp
builder.Services.AddScoped<ISessionHostRepository, SessionHostRepository>();
builder.Services.AddScoped<IPegboardRepository, PegboardRepository>();
builder.Services.AddSingleton<smash_dates.Services.Pegboard.IPegboardEventPublisher, smash_dates.Services.Pegboard.PegboardEventPublisher>();
```
(`PegboardFiller` and `GameMakeup` are static — no registration. The publisher is a **singleton** so all requests share the same fan-out.)

- [ ] **Step 2: Add the `using`s** at the top with the other endpoint usings:

```csharp
using smash_dates.Endpoints.SessionHosts;
using smash_dates.Endpoints.Pegboard;
```

- [ ] **Step 3: Map the endpoint groups** — after `app.MapTransferEndpoints();` add:

```csharp
app.MapSessionHostEndpoints();
app.MapPegboardEndpoints();
```

- [ ] **Step 4: Build** (will fail until the endpoint files exist — that's expected; the next tasks create them, then this compiles).

Run: `dotnet build 2>&1 | tail -5`
Expected after Task 12 & 13 land: builds clean. For now, proceed to Task 12.

### Task 12: SessionHost endpoints (grant / revoke / list)

ClubAdmin-only management of the host role. Mirrors `Endpoints/ClubAdmins/`.

**Files:**
- Create: `Endpoints/SessionHosts/SessionHostEndpoints.cs`, `GrantSessionHostEndpoint.cs`, `RevokeSessionHostEndpoint.cs`, `ListSessionHostsEndpoint.cs`
- Test: `tests/smash-dates.IntegrationTests/Endpoints/SessionHostEndpointsTests.cs`

- [ ] **Step 1: Write the group**

`Endpoints/SessionHosts/SessionHostEndpoints.cs`:
```csharp
namespace smash_dates.Endpoints.SessionHosts;

public static class SessionHostEndpoints
{
    public static IEndpointRouteBuilder MapSessionHostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/session-hosts").RequireAuthorization();
        group.MapGrantSessionHostEndpoint();
        group.MapRevokeSessionHostEndpoint();
        group.MapListSessionHostsEndpoint();
        return app;
    }
}
```

- [ ] **Step 2: Write the three endpoints**

`Endpoints/SessionHosts/GrantSessionHostEndpoint.cs`:
```csharp
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.SessionHosts;

public static class GrantSessionHostEndpoint
{
    public sealed record GrantRequest(Guid UserId);

    public static IEndpointRouteBuilder MapGrantSessionHostEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, GrantRequest request, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;
        if (request.UserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "UserId is required");

        try
        {
            await hosts.GrantAsync(clubId, request.UserId, principal.UserId(), ct);
            return Results.NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == "23503")
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "UserId references unknown user");
        }
    }
}
```

`Endpoints/SessionHosts/RevokeSessionHostEndpoint.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.SessionHosts;

public static class RevokeSessionHostEndpoint
{
    public static IEndpointRouteBuilder MapRevokeSessionHostEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{userId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid userId, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;
        // No last-host protection (CONTEXT.md): a club may have zero hosts.
        return await hosts.RevokeAsync(clubId, userId, ct) ? Results.NoContent() : Results.NotFound();
    }
}
```

`Endpoints/SessionHosts/ListSessionHostsEndpoint.cs`:
```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.SessionHosts;

public static class ListSessionHostsEndpoint
{
    public sealed record HostDto(Guid UserId, System.DateTime GrantedAt);

    public static IEndpointRouteBuilder MapListSessionHostsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, ISessionHostRepository hosts, CancellationToken ct)
    {
        var rows = await hosts.ListByClubAsync(clubId, ct);
        return Results.Ok(rows.Select(h => new HostDto(h.UserId, h.GrantedAt)));
    }
}
```

- [ ] **Step 3: Write integration tests**

`tests/smash-dates.IntegrationTests/Endpoints/SessionHostEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class SessionHostEndpointsTests : IntegrationTestBase
{
    public SessionHostEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Grant_AsClubAdmin_ThenList_ShowsHost()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var host = await Seeder.CreateUserAsync("host@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var grant = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/session-hosts", new { userId = host.Id });
        grant.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await Client.GetFromJsonAsync<List<HostRow>>($"/api/clubs/{clubId}/session-hosts");
        list!.Should().ContainSingle(h => h.UserId == host.Id);
    }

    [Fact]
    public async Task Grant_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateUserAsync("nobody@example.com", "correct-horse-battery");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "correct-horse-battery" });

        var grant = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/session-hosts", new { userId = Guid.NewGuid() });
        grant.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record HostRow(Guid UserId, DateTime GrantedAt);
}
```

- [ ] **Step 4: Build, run tests**

Run: `dotnet build 2>&1 | tail -5` — note: still fails until Task 13 maps `MapPegboardEndpoints`. To compile this task alone, temporarily comment the `app.MapPegboardEndpoints();` line, OR implement Task 13 before building. **Recommended:** implement Task 13's files too, then build once.
After Task 13: `dotnet test --filter "FullyQualifiedName~SessionHostEndpointsTests" 2>&1 | tail -20`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Endpoints/SessionHosts/ tests/smash-dates.IntegrationTests/Endpoints/SessionHostEndpointsTests.cs
git commit -m "feat(pegboard): SessionHost grant/revoke/list endpoints"
```

### Task 13a: Pegboard group, shared guard, session lifecycle, board & SSE

**Files:**
- Create: `Endpoints/Pegboard/PegboardEndpoints.cs`, `PegboardGuards.cs`, `OpenSessionEndpoint.cs`, `CloseSessionEndpoint.cs`, `GetSessionEndpoint.cs`, `ListSessionsEndpoint.cs`, `GetBoardEndpoint.cs`, `StreamBoardEndpoint.cs`
- Test: `tests/smash-dates.IntegrationTests/Endpoints/PegboardSessionEndpointsTests.cs`

- [ ] **Step 1: Write the group**

`Endpoints/Pegboard/PegboardEndpoints.cs`:
```csharp
namespace smash_dates.Endpoints.Pegboard;

public static class PegboardEndpoints
{
    public static IEndpointRouteBuilder MapPegboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/pegboard/sessions").RequireAuthorization();

        // Reads (any authenticated user)
        group.MapListSessionsEndpoint();
        group.MapGetSessionEndpoint();
        group.MapGetBoardEndpoint();
        group.MapStreamBoardEndpoint();

        // Lifecycle (host/admin)
        group.MapOpenSessionEndpoint();
        group.MapCloseSessionEndpoint();

        // Courts / attendances / games (host/admin) — Task 13b & 13c
        group.MapAddCourtEndpoint();
        group.MapRemoveCourtEndpoint();
        group.MapAddAttendanceEndpoint();
        group.MapSetAttendanceStatusEndpoint();
        group.MapRemoveAttendanceEndpoint();
        group.MapSuggestFillEndpoint();
        group.MapStartGameEndpoint();
        group.MapFinishGameEndpoint();
        group.MapCancelGameEndpoint();
        return app;
    }
}
```

- [ ] **Step 2: Write the shared guard** (DRY the "load session, must belong to club, must be Open, caller may run it" precondition used by every mutation)

`Endpoints/Pegboard/PegboardGuards.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

public static class PegboardGuards
{
    // Returns (session, null) when the caller may mutate an Open session of this club,
    // else (null, errorResult). 404 if missing/club-mismatch, 409 if closed, 401/403 if not allowed.
    public static async Task<(PegboardSession? Session, IResult? Error)> LoadOpenForMutationAsync(
        Guid clubId, Guid sessionId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        var session = await pegboard.GetSessionAsync(sessionId, ct);
        if (session is null || session.ClubId != clubId) return (null, Results.NotFound());

        var authz = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct);
        if (authz is not null) return (null, authz);

        if (session.Status != PegboardSessionStatus.Open)
            return (null, Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Session is not open"));

        return (session, null);
    }
}
```

- [ ] **Step 3: Write the session lifecycle + read endpoints**

`Endpoints/Pegboard/OpenSessionEndpoint.cs`:
```csharp
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Pegboard;

public static class OpenSessionEndpoint
{
    private const int MaxNameLength = 200;
    public sealed record OpenRequest(string Name);
    public sealed record SessionDto(Guid Id, Guid ClubId, string Name, string Status);

    public static IEndpointRouteBuilder MapOpenSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, OpenRequest request, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardRepository pegboard, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await SessionAuthorizer.RequireSessionRunnerAsync(principal, clubId, admins, hosts, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        try
        {
            var id = await pegboard.OpenAsync(clubId, name, principal.UserId()!.Value, ct);
            return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{id}",
                new SessionDto(id, clubId, name, "Open"));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "This club already has an open session");
        }
    }
}
```

`Endpoints/Pegboard/CloseSessionEndpoint.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class CloseSessionEndpoint
{
    public static IEndpointRouteBuilder MapCloseSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/close", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        await pegboard.CloseAsync(sessionId, ct);
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
```

`Endpoints/Pegboard/GetSessionEndpoint.cs`:
```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Pegboard;

public static class GetSessionEndpoint
{
    public sealed record SessionDto(Guid Id, Guid ClubId, string Name, string Status, System.DateTime OpenedAt, System.DateTime? ClosedAt);

    public static IEndpointRouteBuilder MapGetSessionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sessionId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, Guid sessionId, IPegboardRepository pegboard, CancellationToken ct)
    {
        var s = await pegboard.GetSessionAsync(sessionId, ct);
        return s is null || s.ClubId != clubId
            ? Results.NotFound()
            : Results.Ok(new SessionDto(s.Id, s.ClubId, s.Name, s.Status.ToString(), s.OpenedAt, s.ClosedAt));
    }
}
```

`Endpoints/Pegboard/ListSessionsEndpoint.cs`:
```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Pegboard;

public static class ListSessionsEndpoint
{
    public sealed record SessionDto(Guid Id, string Name, string Status, System.DateTime OpenedAt, System.DateTime? ClosedAt);

    public static IEndpointRouteBuilder MapListSessionsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, IPegboardRepository pegboard, CancellationToken ct)
    {
        var rows = await pegboard.ListByClubAsync(clubId, ct);
        return Results.Ok(rows.Select(s => new SessionDto(s.Id, s.Name, s.Status.ToString(), s.OpenedAt, s.ClosedAt)));
    }
}
```

`Endpoints/Pegboard/GetBoardEndpoint.cs` — assembles the full board read for viewers:
```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Pegboard;

public static class GetBoardEndpoint
{
    public static IEndpointRouteBuilder MapGetBoardEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sessionId:guid}/board", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, Guid sessionId, IPegboardRepository pegboard, CancellationToken ct)
    {
        var board = await pegboard.GetBoardAsync(sessionId, ct);
        // BoardView already carries the session; ensure it belongs to this club.
        return board is null || board.Session.ClubId != clubId ? Results.NotFound() : Results.Ok(board);
    }
}
```
> The `BoardView` record (and its nested DTOs) serialise directly; enums serialise as names (configured in `Program.cs`). The client types in `pegboard.api.ts` must match these property names exactly.

`Endpoints/Pegboard/StreamBoardEndpoint.cs` — SSE stream (ADR 0004):
```csharp
using System.Text;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class StreamBoardEndpoint
{
    public static IEndpointRouteBuilder MapStreamBoardEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{sessionId:guid}/stream", Handle);
        return app;
    }

    private static async Task Handle(
        HttpContext http, Guid clubId, Guid sessionId,
        IPegboardRepository pegboard, IPegboardEventPublisher events, CancellationToken ct)
    {
        var session = await pegboard.GetSessionAsync(sessionId, ct);
        if (session is null || session.ClubId != clubId)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        http.Response.Headers.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no"; // disable proxy buffering

        var reader = events.Subscribe(sessionId);
        try
        {
            // Tell the client to (re)load the board immediately on connect.
            await WriteEventAsync(http, ct);
            while (await reader.WaitToReadAsync(ct))
            {
                while (reader.TryRead(out _)) { /* coalesce */ }
                await WriteEventAsync(http, ct);
            }
        }
        catch (OperationCanceledException) { /* client disconnected */ }
        finally
        {
            events.Unsubscribe(sessionId, reader);
        }
    }

    private static async Task WriteEventAsync(HttpContext http, CancellationToken ct)
    {
        // Content-free signal — the client re-fetches /board on each event (see ADR 0004).
        await http.Response.WriteAsync("event: board-changed\ndata: 1\n\n", Encoding.UTF8, ct);
        await http.Response.Body.FlushAsync(ct);
    }
}
```

- [ ] **Step 4: Write integration tests**

`tests/smash-dates.IntegrationTests/Endpoints/PegboardSessionEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class PegboardSessionEndpointsTests : IntegrationTestBase
{
    public PegboardSessionEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    private async Task<Guid> LoginSysAdminAndClub()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        return clubId;
    }

    [Fact]
    public async Task Open_AsSystemAdmin_Returns201_ThenSecondOpen_Returns409()
    {
        var clubId = await LoginSysAdminAndClub();
        var first = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tuesday" });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Wednesday" });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Close_ThenMutation_Returns409()
    {
        var clubId = await LoginSysAdminAndClub();
        var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tuesday" });
        var session = await open.Content.ReadFromJsonAsync<SessionRow>();

        var close = await Client.PostAsync($"/api/clubs/{clubId}/pegboard/sessions/{session!.Id}/close", null);
        close.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var addCourt = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{session.Id}/courts", new { label = "Court 1" });
        addCourt.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record SessionRow(Guid Id, Guid ClubId, string Name, string Status);
}
```

- [ ] **Step 5: Build & run** (after Task 13b & 13c create the remaining endpoint files referenced by the group; build then)

Run: `dotnet build 2>&1 | tail -5` → clean once 13b/13c land.
Run: `dotnet test --filter "FullyQualifiedName~PegboardSessionEndpointsTests" 2>&1 | tail -20` → PASS.

- [ ] **Step 6: Commit**

```bash
git add Endpoints/Pegboard/PegboardEndpoints.cs Endpoints/Pegboard/PegboardGuards.cs Endpoints/Pegboard/OpenSessionEndpoint.cs Endpoints/Pegboard/CloseSessionEndpoint.cs Endpoints/Pegboard/GetSessionEndpoint.cs Endpoints/Pegboard/ListSessionsEndpoint.cs Endpoints/Pegboard/GetBoardEndpoint.cs Endpoints/Pegboard/StreamBoardEndpoint.cs tests/smash-dates.IntegrationTests/Endpoints/PegboardSessionEndpointsTests.cs
git commit -m "feat(pegboard): session lifecycle, board read, SSE stream endpoints"
```

### Task 13b: Court & attendance endpoints

**Files:**
- Create: `Endpoints/Pegboard/AddCourtEndpoint.cs`, `RemoveCourtEndpoint.cs`, `AddAttendanceEndpoint.cs`, `SetAttendanceStatusEndpoint.cs`, `RemoveAttendanceEndpoint.cs`
- Test: `tests/smash-dates.IntegrationTests/Endpoints/PegboardCourtAttendanceEndpointsTests.cs`

Every mutation handler follows the same shape: call `PegboardGuards.LoadOpenForMutationAsync(...)`, bail on `error`, do the work, then `events.Publish(sessionId)`. All take `IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts, IPegboardEventPublisher events, ClaimsPrincipal principal, CancellationToken ct`.

- [ ] **Step 1: Courts**

`Endpoints/Pegboard/AddCourtEndpoint.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class AddCourtEndpoint
{
    private const int MaxLabel = 50;
    public sealed record AddCourtRequest(string Label);
    public sealed record CourtDto(Guid Id, string Label);

    public static IEndpointRouteBuilder MapAddCourtEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/courts", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, AddCourtRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        var label = (request.Label ?? string.Empty).Trim();
        if (label.Length == 0 || label.Length > MaxLabel)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid label");

        var id = await pegboard.AddCourtAsync(sessionId, label, ct);
        events.Publish(sessionId);
        return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/courts/{id}", new CourtDto(id, label));
    }
}
```

`Endpoints/Pegboard/RemoveCourtEndpoint.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class RemoveCourtEndpoint
{
    public static IEndpointRouteBuilder MapRemoveCourtEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{sessionId:guid}/courts/{courtId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid courtId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        var court = await pegboard.GetCourtAsync(courtId, ct);
        if (court is null || court.SessionId != sessionId) return Results.NotFound();
        if (await pegboard.HasActiveGameOnCourtAsync(courtId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Court has an active game");

        await pegboard.RemoveCourtAsync(courtId, ct);
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
```

- [ ] **Step 2: Attendances**

`Endpoints/Pegboard/AddAttendanceEndpoint.cs` — roster player (copies gender/grade from the Player; player must be affiliated with the club) OR a guest (name + gender + optional grade):
```csharp
using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class AddAttendanceEndpoint
{
    private const int MaxName = 100;
    public sealed record AddRequest(Guid? PlayerId, string? GuestName, string? Gender, int? Grade);
    public sealed record AttendanceDto(Guid Id);

    public static IEndpointRouteBuilder MapAddAttendanceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/attendances", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, AddRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPlayerRepository players, IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (request.Grade is < 1 or > 5)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Grade must be 1-5");

        Guid id;
        if (request.PlayerId is { } playerId)
        {
            var player = await players.GetByIdAsync(playerId, ct);
            if (player is null) return Results.NotFound();
            // Must be affiliated with this club (Member or Visitor).
            if (await players.GetLinkAsync(playerId, clubId, ct) is null)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Player is not affiliated with this club");
            // Grade defaults to the player's stored grade; request may override for the night.
            var grade = request.Grade ?? player.Grade;
            try
            {
                id = await pegboard.AddPlayerAttendanceAsync(sessionId, playerId, player.Gender, grade, ct);
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Player is already on the board");
            }
        }
        else
        {
            var name = (request.GuestName ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > MaxName)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Guest name required");
            if (!Enum.TryParse<Gender>(request.Gender, out var gender))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Guest gender required (Male/Female)");
            id = await pegboard.AddGuestAttendanceAsync(sessionId, name, gender, request.Grade, ct);
        }

        events.Publish(sessionId);
        return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/attendances/{id}", new AttendanceDto(id));
    }
}
```

`Endpoints/Pegboard/SetAttendanceStatusEndpoint.cs` — move between Waiting / Resting / Left (and Waiting↔Resting). Cannot set status while in an active game; cannot manually set `Playing` (that happens via start-game):
```csharp
using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class SetAttendanceStatusEndpoint
{
    public sealed record SetStatusRequest(string Status);

    public static IEndpointRouteBuilder MapSetAttendanceStatusEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/{sessionId:guid}/attendances/{attendanceId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid attendanceId, SetStatusRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (!Enum.TryParse<AttendanceStatus>(request.Status, out var status)
            || status is AttendanceStatus.Playing)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Status must be Waiting, Resting or Left");

        var att = await pegboard.GetAttendanceAsync(attendanceId, ct);
        if (att is null || att.SessionId != sessionId) return Results.NotFound();
        if (await pegboard.IsInActiveGameAsync(attendanceId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Attendee is in an active game");

        await pegboard.SetAttendanceStatusAsync(attendanceId, status, ct);
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
```

`Endpoints/Pegboard/RemoveAttendanceEndpoint.cs` — remove a peg entirely (blocked while in an active game):
```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class RemoveAttendanceEndpoint
{
    public static IEndpointRouteBuilder MapRemoveAttendanceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{sessionId:guid}/attendances/{attendanceId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid attendanceId, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        var att = await pegboard.GetAttendanceAsync(attendanceId, ct);
        if (att is null || att.SessionId != sessionId) return Results.NotFound();
        if (await pegboard.IsInActiveGameAsync(attendanceId, ct))
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Attendee is in an active game");

        await pegboard.RemoveAttendanceAsync(attendanceId, ct);
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
```

- [ ] **Step 3: Integration tests**

`tests/smash-dates.IntegrationTests/Endpoints/PegboardCourtAttendanceEndpointsTests.cs` — cover: add court 201; add guest attendance 201; add court to closed session 409 (already covered in 13a but ok); set status to `Playing` → 400. Use the `LoginAsSystemAdminAsync` + open-session helper pattern from Task 13a. Example fact:
```csharp
[Fact]
public async Task AddGuestAttendance_Returns201()
{
    var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
    await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
    var open = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" });
    var sid = (await open.Content.ReadFromJsonAsync<Row>())!.Id;

    var add = await Client.PostAsJsonAsync(
        $"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances",
        new { guestName = "Alice", gender = "Female", grade = 2 });

    add.StatusCode.Should().Be(System.Net.HttpStatusCode.Created);
}
private sealed record Row(Guid Id, Guid ClubId, string Name, string Status);
```

- [ ] **Step 4: Build & run**

Run: `dotnet test --filter "FullyQualifiedName~PegboardCourtAttendanceEndpointsTests" 2>&1 | tail -20` → PASS (after 13c lands and the project builds).

- [ ] **Step 5: Commit**

```bash
git add Endpoints/Pegboard/AddCourtEndpoint.cs Endpoints/Pegboard/RemoveCourtEndpoint.cs Endpoints/Pegboard/AddAttendanceEndpoint.cs Endpoints/Pegboard/SetAttendanceStatusEndpoint.cs Endpoints/Pegboard/RemoveAttendanceEndpoint.cs tests/smash-dates.IntegrationTests/Endpoints/PegboardCourtAttendanceEndpointsTests.cs
git commit -m "feat(pegboard): court & attendance endpoints"
```

### Task 13c: Game endpoints (start / finish / cancel) + suggest fill

**Files:**
- Create: `Endpoints/Pegboard/StartGameEndpoint.cs`, `FinishGameEndpoint.cs`, `CancelGameEndpoint.cs`, `SuggestFillEndpoint.cs`
- Test: `tests/smash-dates.IntegrationTests/Endpoints/PegboardGameEndpointsTests.cs`

- [ ] **Step 1: Start game** — validates side sizes for the type, that every attendance is a `Waiting` member of the session, court belongs to the session; sets a non-blocking `warning` when the gender makeup breaks the type's rule (CONTEXT.md: warn, host overrides).

`Endpoints/Pegboard/StartGameEndpoint.cs`:
```csharp
using System.Security.Claims;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class StartGameEndpoint
{
    public sealed record StartRequest(string Type, List<Guid> SideA, List<Guid> SideB);
    public sealed record StartResponse(Guid Id, bool MakeupWarning);

    public static IEndpointRouteBuilder MapStartGameEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/games", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, StartRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, [Microsoft.AspNetCore.Mvc.FromQuery] Guid courtId, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (!Enum.TryParse<GameType>(request.Type, out var type))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid game type");

        var size = GameMakeup.SideSize(type);
        if (request.SideA.Count != size || request.SideB.Count != size)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Each side needs {size} player(s) for {type}");

        var court = await pegboard.GetCourtAsync(courtId, ct);
        if (court is null || court.SessionId != sessionId) return Results.NotFound();

        // All attendances must be Waiting members of this session; collect genders for the makeup check.
        var ids = request.SideA.Concat(request.SideB).ToList();
        if (ids.Distinct().Count() != ids.Count)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "An attendee is listed twice");

        var gendersA = new List<Gender>();
        var gendersB = new List<Gender>();
        foreach (var (sideIds, sink) in new[] { (request.SideA, gendersA), (request.SideB, gendersB) })
        {
            foreach (var id in sideIds)
            {
                var att = await pegboard.GetAttendanceAsync(id, ct);
                if (att is null || att.SessionId != sessionId) return Results.NotFound();
                if (att.Status != AttendanceStatus.Waiting)
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "An attendee is not available");
                sink.Add(att.Gender);
            }
        }

        var warning = !GameMakeup.IsValid(type, gendersA, gendersB);

        try
        {
            var id = await pegboard.StartGameAsync(sessionId, courtId, type, request.SideA, request.SideB, ct);
            events.Publish(sessionId);
            return Results.Created($"/api/clubs/{clubId}/pegboard/sessions/{sessionId}/games/{id}",
                new StartResponse(id, warning));
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Court already has an active game");
        }
    }
}
```
> `courtId` comes from the query string (`POST .../games?courtId=...`) so the route stays clean; the client sends it as a query param. Alternatively fold `CourtId` into `StartRequest` — if you do, drop the `[FromQuery]` param and read `request.CourtId`. Pick one and keep `pegboard.api.ts` consistent.

- [ ] **Step 2: Finish & cancel**

`Endpoints/Pegboard/FinishGameEndpoint.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class FinishGameEndpoint
{
    private const int MaxScore = 30;
    public sealed record FinishRequest(string WinnerSide, string? Score);

    public static IEndpointRouteBuilder MapFinishGameEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/games/{gameId:guid}/finish", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, Guid gameId, FinishRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts,
        IPegboardEventPublisher events, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (!Enum.TryParse<GameSide>(request.WinnerSide, out var winner))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "WinnerSide must be A or B");
        var score = string.IsNullOrWhiteSpace(request.Score) ? null : request.Score!.Trim();
        if (score is { Length: > MaxScore })
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Score too long");

        var game = await pegboard.GetGameAsync(gameId, ct);
        if (game is null || game.SessionId != sessionId) return Results.NotFound();

        var ok = await pegboard.FinishGameAsync(gameId, winner, score, ct);
        if (!ok) return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Game is not active");
        events.Publish(sessionId);
        return Results.NoContent();
    }
}
```

`Endpoints/Pegboard/CancelGameEndpoint.cs` — same shape as finish, but calls `pegboard.CancelGameAsync(gameId, ct)`, takes no body, route `POST /{sessionId:guid}/games/{gameId:guid}/cancel`, returns 204 (or 409 "Game is not active"). Copy `FinishGameEndpoint` and remove the winner/score handling.

- [ ] **Step 3: Suggest fill** — builds the candidate pool from the waiting queue and returns a proposed lineup (or 409 if none can be formed). Does not mutate, does not publish.

`Endpoints/Pegboard/SuggestFillEndpoint.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.Pegboard;

namespace smash_dates.Endpoints.Pegboard;

public static class SuggestFillEndpoint
{
    public sealed record SuggestRequest(string Type);
    public sealed record SuggestResponse(List<Guid> SideA, List<Guid> SideB);

    public static IEndpointRouteBuilder MapSuggestFillEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{sessionId:guid}/suggest", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid sessionId, SuggestRequest request, ClaimsPrincipal principal,
        IPegboardRepository pegboard, IClubAdminRepository admins, ISessionHostRepository hosts, CancellationToken ct)
    {
        var (_, error) = await PegboardGuards.LoadOpenForMutationAsync(clubId, sessionId, principal, pegboard, admins, hosts, ct);
        if (error is not null) return error;

        if (!Enum.TryParse<GameType>(request.Type, out var type))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid game type");

        var waiting = await pegboard.ListWaitingAsync(sessionId, ct);
        var pool = waiting.Select((w, i) => new FillCandidate(w.Id, w.Gender, w.Grade, i)).ToList();
        var pairs = await pegboard.ListPlayedPairsAsync(sessionId, ct);

        var suggestion = PegboardFiller.Suggest(type, pool, pairs);
        return suggestion is null
            ? Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Not enough waiting players to form this game")
            : Results.Ok(new SuggestResponse(suggestion.SideA.ToList(), suggestion.SideB.ToList()));
    }
}
```

- [ ] **Step 4: Integration test** (the happy spine through HTTP)

`tests/smash-dates.IntegrationTests/Endpoints/PegboardGameEndpointsTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class PegboardGameEndpointsTests : IntegrationTestBase
{
    public PegboardGameEndpointsTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task StartSingles_ThenFinish_FreesCourtAndRequeuesPlayers()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var sid = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions", new { name = "Tue" }))
            .Content.ReadFromJsonAsync<SessionRow>())!.Id;
        var courtId = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{sid}/courts", new { label = "C1" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;
        var a1 = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances", new { guestName = "Alice", gender = "Female" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;
        var a2 = (await (await Client.PostAsJsonAsync($"/api/clubs/{clubId}/pegboard/sessions/{sid}/attendances", new { guestName = "Bob", gender = "Male" }))
            .Content.ReadFromJsonAsync<IdRow>())!.Id;

        var start = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/games?courtId={courtId}",
            new { type = "Singles", sideA = new[] { a1 }, sideB = new[] { a2 } });
        start.StatusCode.Should().Be(HttpStatusCode.Created);
        var gameId = (await start.Content.ReadFromJsonAsync<StartRow>())!.Id;

        var finish = await Client.PostAsJsonAsync(
            $"/api/clubs/{clubId}/pegboard/sessions/{sid}/games/{gameId}/finish",
            new { winnerSide = "A", score = "21-15" });
        finish.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var board = await Client.GetFromJsonAsync<BoardRow>($"/api/clubs/{clubId}/pegboard/sessions/{sid}/board");
        board!.Courts.Single().ActiveGame.Should().BeNull();
        board.Attendees.Should().OnlyContain(x => x.Status == "Waiting");
    }

    private sealed record SessionRow(Guid Id);
    private sealed record IdRow(Guid Id);
    private sealed record StartRow(Guid Id, bool MakeupWarning);
    private sealed record BoardRow(List<CourtRow> Courts, List<AttRow> Attendees);
    private sealed record CourtRow(Guid Id, string Label, object? ActiveGame);
    private sealed record AttRow(Guid Id, string Status);
}
```

- [ ] **Step 5: Build & run the whole pegboard endpoint suite**

Run: `dotnet build 2>&1 | tail -5` → clean (all `Map...` referenced files now exist).
Run: `dotnet test --filter "FullyQualifiedName~Pegboard" 2>&1 | tail -30` → all PASS.

- [ ] **Step 6: Commit**

```bash
git add Endpoints/Pegboard/StartGameEndpoint.cs Endpoints/Pegboard/FinishGameEndpoint.cs Endpoints/Pegboard/CancelGameEndpoint.cs Endpoints/Pegboard/SuggestFillEndpoint.cs tests/smash-dates.IntegrationTests/Endpoints/PegboardGameEndpointsTests.cs
git commit -m "feat(pegboard): game start/finish/cancel + suggest-fill endpoints"
```

### Task 14: Player grade endpoint

Lets a ClubAdmin set/clear a player's persistent grade. Mirrors the existing club-players routes.

**Files:**
- Create: `Endpoints/Players/SetPlayerGradeEndpoint.cs`
- Modify: `Endpoints/Players/ClubPlayersEndpoints.cs` (map it; add `grade` to `PlayerDto` + the roster list projection)
- Test: add a fact to an existing club-players test, or create `tests/smash-dates.IntegrationTests/Endpoints/SetPlayerGradeEndpointTests.cs`

- [ ] **Step 1: Endpoint**

`Endpoints/Players/SetPlayerGradeEndpoint.cs`:
```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Players;

public static class SetPlayerGradeEndpoint
{
    public sealed record SetGradeRequest(int? Grade);

    public static IEndpointRouteBuilder MapSetPlayerGradeEndpoint(this IEndpointRouteBuilder app)
    {
        // Mapped on the existing /api/clubs/{clubId}/players group in ClubPlayersEndpoints.
        app.MapPatch("/{playerId:guid}/grade", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId, Guid playerId, SetGradeRequest request, ClaimsPrincipal principal,
        IClubRepository clubs, IClubAdminRepository admins, IPlayerRepository players, CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();
        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;
        if (request.Grade is < 1 or > 5)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Grade must be 1-5 or null");

        return await players.SetGradeAsync(playerId, request.Grade, ct)
            ? Results.NoContent() : Results.NotFound();
    }
}
```

- [ ] **Step 2: Wire it in `ClubPlayersEndpoints.cs`**

In the `group` block (the `/api/clubs/{clubId:guid}/players` group) add:
```csharp
        group.MapSetPlayerGradeEndpoint();
```
Add `int? Grade` to the `PlayerDto` record and include `p.Grade` when projecting roster rows (the `ListClubPlayers` handler maps `PlayerClubView` → `PlayerDto`; `PlayerClubView` now has `Grade`). Update `new PlayerDto(...)` call sites to pass the grade.

- [ ] **Step 3: Test** — set grade as ClubAdmin returns 204; non-admin returns 403; grade 6 returns 400. Mirror `CreateVenueEndpointTests` auth setup.

- [ ] **Step 4: Build & run**

Run: `dotnet test --filter "FullyQualifiedName~SetPlayerGrade" 2>&1 | tail -20` → PASS.

- [ ] **Step 5: Commit**

```bash
git add Endpoints/Players/SetPlayerGradeEndpoint.cs Endpoints/Players/ClubPlayersEndpoints.cs tests/smash-dates.IntegrationTests/Endpoints/SetPlayerGradeEndpointTests.cs
git commit -m "feat(pegboard): set player grade endpoint + grade in roster DTO"
```

---

## Phase 5 — Frontend

### Task 15: `pegboard.api.ts` — DTOs + API service + SSE stream

DTO property names MUST match the backend records (enums serialise as names). The SSE method returns an `Observable` that emits on each `board-changed` event so the page can re-fetch the board.

**Files:**
- Create: `ClientApp/src/app/features/admin/pegboard.api.ts`
- Test: `ClientApp/src/app/features/admin/pegboard.api.spec.ts`

- [ ] **Step 1: Write the API service**

`ClientApp/src/app/features/admin/pegboard.api.ts`:
```ts
import { HttpClient } from '@angular/common/http';
import { Injectable, NgZone, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Gender } from './clubs.api';

export type PegSessionStatus = 'Open' | 'Closed';
export type AttendanceStatus = 'Waiting' | 'Playing' | 'Resting' | 'Left';
export type GameType = 'Singles' | 'Doubles' | 'Mixed' | 'Funny';
export type GameSide = 'A' | 'B';

export interface SessionSummary {
  id: string;
  name: string;
  status: PegSessionStatus;
  openedAt: string;
  closedAt: string | null;
}

export interface BoardGamePlayer {
  attendanceId: string;
  displayName: string;
  gender: Gender;
  grade: number | null;
  side: GameSide;
}
export interface BoardGame {
  id: string;
  type: GameType;
  players: BoardGamePlayer[];
}
export interface BoardCourt {
  id: string;
  label: string;
  activeGame: BoardGame | null;
}
export interface BoardAttendee {
  id: string;
  playerId: string | null;
  displayName: string;
  gender: Gender;
  grade: number | null;
  status: AttendanceStatus;
  waitingSince: string;
  gamesPlayed: number;
  gamesWon: number;
}
export interface BoardView {
  session: { id: string; clubId: string; name: string; status: PegSessionStatus };
  courts: BoardCourt[];
  attendees: BoardAttendee[];
}

export interface FillSuggestion { sideA: string[]; sideB: string[]; }

@Injectable({ providedIn: 'root' })
export class PegboardApi {
  private readonly http = inject(HttpClient);
  private readonly zone = inject(NgZone);

  private base(clubId: string): string {
    return `/api/clubs/${clubId}/pegboard/sessions`;
  }

  listSessions(clubId: string): Observable<SessionSummary[]> {
    return this.http.get<SessionSummary[]>(this.base(clubId));
  }
  openSession(clubId: string, name: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.base(clubId), { name });
  }
  closeSession(clubId: string, sessionId: string): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/close`, null);
  }
  getBoard(clubId: string, sessionId: string): Observable<BoardView> {
    return this.http.get<BoardView>(`${this.base(clubId)}/${sessionId}/board`);
  }

  addCourt(clubId: string, sessionId: string, label: string): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/courts`, { label });
  }
  removeCourt(clubId: string, sessionId: string, courtId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(clubId)}/${sessionId}/courts/${courtId}`);
  }
  addGuest(clubId: string, sessionId: string, guestName: string, gender: Gender, grade: number | null): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/attendances`, { guestName, gender, grade });
  }
  addPlayer(clubId: string, sessionId: string, playerId: string, grade: number | null): Observable<unknown> {
    return this.http.post(`${this.base(clubId)}/${sessionId}/attendances`, { playerId, grade });
  }
  setAttendanceStatus(clubId: string, sessionId: string, attendanceId: string, status: AttendanceStatus): Observable<void> {
    return this.http.patch<void>(`${this.base(clubId)}/${sessionId}/attendances/${attendanceId}`, { status });
  }
  removeAttendance(clubId: string, sessionId: string, attendanceId: string): Observable<void> {
    return this.http.delete<void>(`${this.base(clubId)}/${sessionId}/attendances/${attendanceId}`);
  }
  suggest(clubId: string, sessionId: string, type: GameType): Observable<FillSuggestion> {
    return this.http.post<FillSuggestion>(`${this.base(clubId)}/${sessionId}/suggest`, { type });
  }
  startGame(clubId: string, sessionId: string, courtId: string, type: GameType, sideA: string[], sideB: string[]): Observable<{ id: string; makeupWarning: boolean }> {
    return this.http.post<{ id: string; makeupWarning: boolean }>(
      `${this.base(clubId)}/${sessionId}/games?courtId=${courtId}`, { type, sideA, sideB });
  }
  finishGame(clubId: string, sessionId: string, gameId: string, winnerSide: GameSide, score: string | null): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/games/${gameId}/finish`, { winnerSide, score });
  }
  cancelGame(clubId: string, sessionId: string, gameId: string): Observable<void> {
    return this.http.post<void>(`${this.base(clubId)}/${sessionId}/games/${gameId}/cancel`, null);
  }

  // SSE: emits once on connect and on each board-changed event. Re-fetch the board on each emission.
  stream(clubId: string, sessionId: string): Observable<void> {
    return new Observable<void>((subscriber) => {
      const es = new EventSource(`${this.base(clubId)}/${sessionId}/stream`, { withCredentials: true });
      const onMsg = () => this.zone.run(() => subscriber.next());
      es.addEventListener('board-changed', onMsg);
      es.onerror = () => { /* EventSource auto-reconnects; ignore transient errors */ };
      return () => es.close();
    });
  }
}
```

- [ ] **Step 2: Write a spec** (`pegboard.api.spec.ts`) using `HttpTestingController` to assert URLs/bodies for `openSession`, `getBoard`, `startGame` (query param), `finishGame`. Mirror an existing `*.api.spec.ts` if present; otherwise use the standard `provideHttpClient()` + `provideHttpClientTesting()` setup.

- [ ] **Step 3: Run frontend tests**

Run: `cd ClientApp && npm test -- --run pegboard.api` (Vitest)
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add ClientApp/src/app/features/admin/pegboard.api.ts ClientApp/src/app/features/admin/pegboard.api.spec.ts
git commit -m "feat(pegboard): Angular API client + SSE stream"
```

### Task 16: Full-screen board page + route

The running board: a courts grid + a waiting-queue column, live via SSE, host controls (add court, add attendee, fill/suggest/start, finish/cancel) shown only to runners; read-only for viewers. **Invoke the `frontend-design` skill before writing the template** to get the brutalist-but-polished look matching the app (font-mono headings, slate light/dark palette, big touch targets for tablet/wall). The structure below is the contract; the skill guides the visual detail.

**Files:**
- Create: `ClientApp/src/app/features/admin/pegboard-board.page.ts`
- Modify: `ClientApp/src/app/features/admin/admin.routes.ts`
- Test: `ClientApp/src/app/features/admin/pegboard-board.page.spec.ts`

- [ ] **Step 1: Add the route**

In `admin.routes.ts`, add (after the `clubs/:id` route):
```ts
  {
    path: 'clubs/:id/pegboard/:sessionId',
    title: 'Pegboard · smash-dates',
    loadComponent: () => import('./pegboard-board.page'),
  },
```

- [ ] **Step 2: Write the component (state + behaviour contract)**

`ClientApp/src/app/features/admin/pegboard-board.page.ts` — key requirements (use signals + `OnPush`; default-export the component class to match the lazy `loadComponent`):
  - Read `clubId` and `sessionId` from `ActivatedRoute`.
  - `board = signal<BoardView | null>(null)`. On init, subscribe to `api.stream(clubId, sessionId)` and on each emission call `api.getBoard(...)` and `board.set(...)`. Unsubscribe on destroy (use `takeUntilDestroyed` or store the subscription).
  - `waiting = computed(() => board()?.attendees.filter(a => a.status === 'Waiting') ?? [])`, similarly `resting`, and a `playing` count. Sort `waiting` by `waitingSince` ascending (server already does, but keep stable).
  - `canRun = input/route-derived boolean`: attempt `getBoard` always works (viewer); to decide whether to SHOW controls, attempt is simplest to gate on the user holding a role — but the app has no client role map for clubs here. Simplest correct approach: always render controls and let the API return 403 (the board is host-facing). **Decision for v1:** show controls always; surface a toast on 403. (A later refinement can fetch the caller's club roles.)
  - Court card: shows `label`; if `activeGame`, list its players grouped by side with a "Finish" (opens winner+score prompt) and "Cancel" button; if empty, a "Fill" control: pick `GameType`, then either **Suggest** (calls `api.suggest`, pre-selects the returned attendees, lets host adjust) or **Manual** (host taps waiting attendees to assign to side A/B) then **Start** (`api.startGame`); show the `makeupWarning` from the response as a non-blocking toast.
  - Add-court button (prompt for label). Add-attendee control (guest: name + gender + optional grade; or pick a club player — for v1, guest entry is the minimum; player pick can reuse `PlayersApi`).
  - Attendee row in the queue shows displayName, gender, grade, gamesPlayed/gamesWon, and Rest/Leave/Remove actions.
  - "Close session" button (confirm) → `api.closeSession` → navigate back to the club Sessions tab.
  - No `new Date()` in templates; compute wait durations in the component with `computed()` off a `now` signal updated by `interval(1000)` (optional; or just show `gamesPlayed`).
  - Use `ConfirmComponent`/`ModalComponent` from `shared/` for confirms and the start/finish dialogs (see how `club-detail.page.ts` imports and uses them).

- [ ] **Step 3: Write a component spec** verifying: renders courts and waiting attendees from a mocked `PegboardApi.getBoard`; clicking "Add court" calls `addCourt`; finishing a game calls `finishGame`. Mock `PegboardApi` with a stub returning `of(...)`; stub `stream` to return `of(void 0)` once.

- [ ] **Step 4: Run frontend tests**

Run: `cd ClientApp && npm test -- --run pegboard-board` → PASS.
Run: `cd ClientApp && npm run build` → builds clean (verifies the lazy route + default export).

- [ ] **Step 5: Commit**

```bash
git add ClientApp/src/app/features/admin/pegboard-board.page.ts ClientApp/src/app/features/admin/pegboard-board.page.spec.ts ClientApp/src/app/features/admin/admin.routes.ts
git commit -m "feat(pegboard): full-screen live board page + route"
```

### Task 17: "Sessions" tab on the club page

Lists past/current sessions and opens a new one; links each to the board route.

**Files:**
- Create: `ClientApp/src/app/features/admin/pegboard-sessions.component.ts`
- Modify: `ClientApp/src/app/features/admin/club-detail.page.ts`

- [ ] **Step 1: Build the tab component**

`pegboard-sessions.component.ts` — a standalone `OnPush` component with `clubId = input.required<string>()`:
  - On init, `api.listSessions(clubId())` into a `sessions = signal<SessionSummary[]>([])`.
  - Show a table/list: name, status badge (reuse `StatusColorPipe` if it covers Open/Closed; otherwise plain badge), openedAt. Each row links via `routerLink` to `['/admin/clubs', clubId(), 'pegboard', s.id]`.
  - "Open session" button (prompt/modal for a name) → `api.openSession(...)` → on success, navigate straight to the board route for the new id. If it 409s ("already open"), show the existing open session's link.
  - Match the visual idiom of the other tab components (e.g. `club-players.component.ts`).

- [ ] **Step 2: Wire the tab into `club-detail.page.ts`**

  - Add `'sessions'` to the tabs list (the `clubTabs()` computed / `TabDef[]`). Find where `clubTabs` is defined and add `{ id: 'sessions', label: 'Sessions' }` (match the existing `TabDef` shape).
  - Import `PegboardSessionsComponent` and add it to the component `imports`.
  - Add the tab panel:
```html
@if (tabs.active() === 'sessions') {
  <section role="tabpanel" id="panel-sessions" aria-labelledby="tab-sessions" class="mt-8">
    <app-pegboard-sessions [clubId]="clubId()" />
  </section>
}
```

- [ ] **Step 3: Run frontend tests + build**

Run: `cd ClientApp && npm test -- --run` → existing + new specs PASS.
Run: `cd ClientApp && npm run build` → clean.

- [ ] **Step 4: Commit**

```bash
git add ClientApp/src/app/features/admin/pegboard-sessions.component.ts ClientApp/src/app/features/admin/club-detail.page.ts
git commit -m "feat(pegboard): Sessions tab on the club page"
```

---

## Phase 6 — Docs & verification

### Task 18: Documentation

`CONTEXT.md` and `docs/adr/0004-...` were already written during design. This task updates `README.md` and reconciles one wording detail.

**Files:**
- Modify: `README.md`, `CONTEXT.md`

- [ ] **Step 1: Reconcile the close-behaviour wording in `CONTEXT.md`**

The implementation ends in-progress games as **Cancelled** (no winner) on close, because a `Finished` game requires a winner. Update the Pegboard Session bullet so the glossary matches behaviour:
- Find: `**Closing** finishes any in-progress Games and makes the board read-only.`
- Replace with: `**Closing** ends any in-progress Games with no result recorded and makes the board read-only.`

- [ ] **Step 2: Add a Features entry to `README.md`**

After the **Players & registrations** feature block (before **Interface**), add:
```markdown
**Club night (pegboard)**
- A live **pegboard session** replaces the physical club-night board: track who turned up (roster players or ad-hoc guests), a fair waiting queue, courts you add/remove on the fly, and the games on them (singles / level doubles / mixed / "funny"), with winner + optional score and per-night stats.
- Fill a free court three ways — **manual**, **suggest**, or **auto-fill** — balancing longest-waiting, valid gender makeup, partner variety and player **grade**.
- Run by a new per-club **Session Host** role (or any club admin); the board streams live to every viewer over **Server-Sent Events** (see [ADR 0004](docs/adr/0004-sse-for-pegboard-live-updates.md)).
```
Also add to the **Background work** line in the Tech stack table is not needed (no new hosted service). Add `Server-Sent Events (pegboard live board)` to the Background/real-time description if desired.

- [ ] **Step 3: Add Screenshots section entries** (images created in Task 19)

In the Screenshots section of `README.md`, after the Clubs entry, add:
```markdown
### Club night (pegboard)
The Sessions tab opens a club night; the full-screen board tracks courts, the waiting queue and live games.

![Pegboard sessions tab](docs/screenshots/pegboard-sessions.png)
![Pegboard live board](docs/screenshots/pegboard-board.png)
```

- [ ] **Step 4: Commit**

```bash
git add README.md CONTEXT.md
git commit -m "docs(pegboard): README features + screenshots refs; reconcile close wording"
```

### Task 19: Screenshots (seeded run, light + dark)

Per `CLAUDE.md`, regenerate screenshots from a seeded run wherever UI changes.

**Files:**
- Create: `docs/screenshots/pegboard-sessions.png`, `docs/screenshots/pegboard-board.png` (and dark variants if the existing convention keeps them — match how `docs/screenshots/dark-mode.png` is handled).

- [ ] **Step 1: Run the app seeded** — use the `run` skill (or `docker compose up -d` + `dotnet run` + `npm run watch` per README). Register the first user (SystemAdmin), create a club, grant yourself ClubAdmin, add a few players with genders/grades.

- [ ] **Step 2: Capture** — open the club page → Sessions tab (screenshot `pegboard-sessions.png`), open a session, add 2 courts + ~6 attendees, start a couple of games (screenshot the board `pegboard-board.png`). Capture light and dark per the existing screenshot convention.

- [ ] **Step 3: Commit**

```bash
git add docs/screenshots/pegboard-sessions.png docs/screenshots/pegboard-board.png
git commit -m "docs(pegboard): screenshots of sessions tab and live board"
```

### Task 20: Full verification

- [ ] **Step 1: Backend tests**

Run: `dotnet test 2>&1 | tail -20`
Expected: all PASS (integration tests need Docker for Testcontainers).

- [ ] **Step 2: Frontend tests + build**

Run: `cd ClientApp && npm test -- --run 2>&1 | tail -20` → PASS.
Run: `cd ClientApp && npm run build 2>&1 | tail -10` → clean.

- [ ] **Step 3: Manual smoke (use the `verify` or `run` skill)** — open a session, add courts/attendees, suggest+start a mixed game (confirm the makeup warning appears for a deliberately wrong makeup), finish with a winner, watch a second browser tab update live via SSE, close the session and confirm the board is read-only.

- [ ] **Step 4: Finishing the branch** — use the `superpowers:finishing-a-development-branch` skill to open the PR.

---

## Self-review (completed by plan author)

- **Spec coverage:** session lifecycle (T7/T13a), one-open-per-club (T1 index/T13a), courts add/remove-if-empty (T7/T13b), attendances player+guest with per-night grade override (T7/T13b), states Waiting/Playing/Resting/Left (T2/T13b), queue wait-order + finish-to-tail (T7), games sides+winner+optional score (T7/T13c), 4 game types + makeup warn-not-block (T8/T13c), cancel-vs-finish (T7/T13c), fill modes manual/suggest/auto with fairness+makeup+variety+grade (T9/T13c/T16), SessionHost role + ClubAdmin implicit + no last-host (T4/T5/T12), view=any-auth / mutate=runner (T13a guards), SSE live (T10/T13a/T15), Player.Grade persistent 1–5 (T1/T3/T6/T14), Sessions tab + full-screen board (T16/T17), docs (T18/T19). All covered.
- **Type consistency:** `PegboardSessionStatus`, `AttendanceStatus`, `GameType`, `GameStatus`, `GameSide` used consistently; `BoardView`/`BoardCourt`/`BoardGame`/`BoardGamePlayer`/`BoardAttendee` names match between `IPegboardRepository`, `GetBoardEndpoint`, and `pegboard.api.ts`; `FillCandidate`/`FillSuggestion` consistent between filler and `SuggestFillEndpoint`.
- **Repository-test infra (CONFIRMED during execution):** repository tests do **not** extend `IntegrationTestBase` for the DB factory — `IntegrationTestBase.Factory` is the `TestWebApplicationFactory` (HTTP host), not an `IDbConnectionFactory`. Mirror `ClubAdminRepositoryTests`: implement `IAsyncLifetime` directly and build an `NpgsqlConnectionFactory` from `fixture.ConnectionString` (via `ConfigurationBuilder`). Apply this to the T6, T7 and T9 repository tests too (ignore the `Factory` placeholder in those task snippets).
- **Dapper mapping (CONFIRMED):** use named private `record` row types for board queries (not positional value-tuples), and **cast in SQL** to match record constructor types — `grade::int` (column is `smallint`) and `count(*)::int` (returns `bigint`); Dapper's record-constructor mapping is strict about `Int16`/`Int64` vs `int`. Property-based entity mapping (e.g. `GetAttendanceAsync`) tolerates the widening, so only the record-constructor queries need casts.
- **Test runner (CONFIRMED):** xUnit v3 on Microsoft.Testing.Platform — `dotnet test --filter` is unreliable here. Run a single class via the built test exe with a single-dash query filter, e.g. `smash-dates.IntegrationTests.exe -filter "/*/*/PegboardRepositoryTests/*"`. The final verification (Task 20) runs the whole suite with plain `dotnet test`.
- **Known seams to confirm during execution (flagged inline):** Dapper positional value-tuple mapping (fall back to named row records if rejected); `StartGame` court id via query vs body (pick one, keep client in step); client role-gating of board controls deferred to API 403 in v1.

