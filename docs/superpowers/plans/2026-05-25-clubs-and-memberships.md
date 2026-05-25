# Slice 2b — Clubs + ClubAdmin Grants + Club–League Memberships

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Club aggregate (open-registry organisation), per-Club role grants mirroring the LeagueAdmin pattern, and the full Club↔League membership lifecycle (invite/accept/decline/withdraw/expel) so a SystemAdmin can onboard real clubs and LeagueAdmins can populate their leagues with member clubs.

**Architecture:**
- Three new tables: `clubs`, `club_admins` (composite PK), and `club_league_memberships` (UUID PK + status text + audit columns). All migrations follow DbUp's numbered SQL convention.
- Mirror slice 2a's role-grant pattern: `ClubAuthorizer.RequireClubAdminAsync` inline helper, identical shape to `LeagueAuthorizer`. Last-admin invariant uses the same single-statement CTE pattern as the post-review fix on the league side.
- Memberships are state-machined via RPC-style POST endpoints (`/accept`, `/decline`, `/withdraw`, `/expel`) rather than `PATCH /memberships/{id}` — clearer authz boundaries and audit trails.
- Open registry visibility: any authenticated user reads any Club. Writes restricted to `ClubAdmin@thisClub` (or `SystemAdmin`).
- Mid-season Withdraw/Expel constraint is **deferred** to the slice that introduces `seasons` and `season_entries`. Today there are no Season Entries, so the block is trivially satisfied; the endpoint code is structured so the constraint slots in as a single check.
- Frontend: new `/admin/clubs` (list + create) and `/admin/clubs/:id` (detail + admin management). League-detail page gains a "Member clubs" section with invite/expel controls. The whole `/admin` section's `systemAdminGuard` is loosened to plain `authGuard` so a `ClubAdmin` can reach `/admin/clubs/:id`; per-action authz remains server-enforced.

**Tech Stack:** .NET 10 minimal API · Dapper · Npgsql · PostgreSQL · DbUp · Angular 21 · xUnit v3 + Microsoft.Testing.Platform.

**Out of scope (later slices):**
- Venues, Blocked Dates, Teams, Seasons, Season Entries.
- Mid-season Withdraw/Expel block (no Season Entry table to consult yet).
- Real email notifications.
- Audit log entries for grants / membership transitions.

**Branch:** `feature/clubs-and-memberships`, branched from `main` (slices 1 + 2a are now merged into `main`).

---

## File Structure

**Created:**
- `Migrations/Scripts/0008_create_clubs.sql`
- `Migrations/Scripts/0009_create_club_admins.sql`
- `Migrations/Scripts/0010_create_club_league_memberships.sql`
- `Models/Club.cs`
- `Models/ClubAdminGrant.cs`
- `Models/ClubLeagueMembership.cs`
- `Models/MembershipStatus.cs` (enum: `Pending | Accepted | Declined | Withdrawn | Expelled`)
- `Repositories/IClubRepository.cs` / `ClubRepository.cs`
- `Repositories/IClubAdminRepository.cs` / `ClubAdminRepository.cs` (mirror `ILeagueAdminRepository`, includes `RevokeUnlessLastAsync`)
- `Repositories/IClubLeagueMembershipRepository.cs` / `ClubLeagueMembershipRepository.cs`
- `Services/Auth/ClubAuthorizer.cs`
- `Endpoints/Clubs/ClubEndpoints.cs`, `CreateClubEndpoint.cs`, `ListClubsEndpoint.cs`, `GetClubEndpoint.cs`, `UpdateClubEndpoint.cs`
- `Endpoints/ClubAdmins/ClubAdminEndpoints.cs`, `ListClubAdminsEndpoint.cs`, `GrantClubAdminEndpoint.cs`, `RevokeClubAdminEndpoint.cs`
- `Endpoints/Memberships/MembershipEndpoints.cs`, `InviteMembershipEndpoint.cs`, `ListLeagueMembershipsEndpoint.cs`, `ListClubMembershipsEndpoint.cs`, `AcceptMembershipEndpoint.cs`, `DeclineMembershipEndpoint.cs`, `WithdrawMembershipEndpoint.cs`, `ExpelMembershipEndpoint.cs`
- Tests for every repository and endpoint above.
- `ClientApp/src/app/features/admin/clubs.api.ts`
- `ClientApp/src/app/features/admin/clubs-list.page.ts`
- `ClientApp/src/app/features/admin/club-detail.page.ts`

**Modified:**
- `Program.cs` — register 3 new repos, map 3 new endpoint groups.
- `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs` — `CreateClubAsync`, `GrantClubAdminAsync`, `CreateMembershipAsync`.
- `ClientApp/src/app/app.routes.ts` — replace `systemAdminGuard` on `/admin` with `authGuard` only.
- `ClientApp/src/app/features/admin/admin.routes.ts` — register `/clubs` and `/clubs/:id` routes.
- `ClientApp/src/app/features/admin/league-detail.page.ts` — add member-clubs section (invite + list).
- `ClientApp/src/app/features/admin/leagues.api.ts` — add membership methods.
- `ClientApp/src/app/features/admin/leagues-list.page.ts` — small: the "Create league" button hidden unless `isSystemAdmin`.
- `README.md` — document the new endpoints.

---

### Task 0: Confirm baseline

- [ ] **Step 1: On a fresh branch with `dotnet test` and `npm test` both green**

```
git status                      # clean
git log -1 --oneline            # main tip
dotnet test
cd ClientApp && npm test && cd ..
```

All green expected.

---

### Task 1: Three schema migrations

**Files:**
- Create: `Migrations/Scripts/0008_create_clubs.sql`
- Create: `Migrations/Scripts/0009_create_club_admins.sql`
- Create: `Migrations/Scripts/0010_create_club_league_memberships.sql`

- [ ] **Step 1: Write `0008_create_clubs.sql`**

```sql
CREATE TABLE clubs (
    id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name          text NOT NULL,
    short_code    text NOT NULL,
    contact_email text NOT NULL,
    notes         text NULL,
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now(),
    CHECK (char_length(short_code) BETWEEN 3 AND 5),
    CHECK (short_code = upper(short_code)),
    CHECK (short_code ~ '^[A-Z0-9]+$')
);

CREATE UNIQUE INDEX ux_clubs_name_lower ON clubs (lower(name));
CREATE UNIQUE INDEX ux_clubs_short_code ON clubs (short_code);
```

- [ ] **Step 2: Write `0009_create_club_admins.sql`**

```sql
CREATE TABLE club_admins (
    club_id     uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    granted_at  timestamptz NOT NULL DEFAULT now(),
    granted_by  uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    PRIMARY KEY (club_id, user_id)
);

CREATE INDEX ix_club_admins_user ON club_admins (user_id);
```

- [ ] **Step 3: Write `0010_create_club_league_memberships.sql`**

```sql
CREATE TABLE club_league_memberships (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    club_id        uuid NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
    league_id      uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    status         text NOT NULL CHECK (status IN ('Pending', 'Accepted', 'Declined', 'Withdrawn', 'Expelled')),
    invited_at     timestamptz NOT NULL DEFAULT now(),
    invited_by     uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    responded_at   timestamptz NULL,
    responded_by   uuid NULL REFERENCES users(id) ON DELETE SET NULL
);

-- Only one non-terminal membership row per (club, league). Terminal statuses
-- (Declined, Withdrawn, Expelled) may be repeated as a re-invite history.
CREATE UNIQUE INDEX ux_club_league_memberships_active
    ON club_league_memberships (club_id, league_id)
    WHERE status IN ('Pending', 'Accepted');

CREATE INDEX ix_club_league_memberships_league ON club_league_memberships (league_id);
CREATE INDEX ix_club_league_memberships_club ON club_league_memberships (club_id);
```

- [ ] **Step 4: Run migrator tests**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*DbMigratorTests"
```

Expect: 2/2 pass.

- [ ] **Step 5: Commit**

```
git add Migrations/Scripts/0008_create_clubs.sql Migrations/Scripts/0009_create_club_admins.sql Migrations/Scripts/0010_create_club_league_memberships.sql
git commit -m "feat(db): add clubs, club_admins, club_league_memberships tables"
```

---

### Task 2: `Club` model + repository

**Files:**
- Create: `Models/Club.cs`
- Create: `Repositories/IClubRepository.cs`
- Create: `Repositories/ClubRepository.cs`
- Create: `tests/smash-dates.IntegrationTests/Repositories/ClubRepositoryTests.cs`
- Modify: `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`

- [ ] **Step 1: Write the model**

`Models/Club.cs`:

```csharp
namespace smash_dates.Models;

public sealed class Club
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ShortCode { get; init; } = string.Empty;
    public string ContactEmail { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
```

- [ ] **Step 2: Write the interface**

`Repositories/IClubRepository.cs`:

```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IClubRepository
{
    Task<Club?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Club>> ListAsync(CancellationToken ct = default);

    // Creates the club row and the initial ClubAdmin grant in a single transaction.
    Task<Guid> CreateWithFirstAdminAsync(
        string name,
        string shortCode,
        string contactEmail,
        string? notes,
        Guid firstAdminUserId,
        Guid grantedBy,
        CancellationToken ct = default);

    Task<bool> UpdateAsync(
        Guid id,
        string name,
        string shortCode,
        string contactEmail,
        string? notes,
        CancellationToken ct = default);
}
```

- [ ] **Step 3: Write the repository**

`Repositories/ClubRepository.cs`:

```csharp
using Dapper;
using Npgsql;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class ClubRepository : IClubRepository
{
    private const string SelectColumns =
        "id, name, short_code, contact_email, notes, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public ClubRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Club?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Club>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM clubs WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<Club>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Club>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM clubs ORDER BY name",
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> CreateWithFirstAdminAsync(
        string name,
        string shortCode,
        string contactEmail,
        string? notes,
        Guid firstAdminUserId,
        Guid grantedBy,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        if (conn is System.Data.Common.DbConnection dbConn)
        {
            await dbConn.OpenAsync(ct);
        }
        else
        {
            conn.Open();
        }

        using var tx = conn.BeginTransaction();

        var id = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO clubs (name, short_code, contact_email, notes)
                  VALUES (@name, @shortCode, @contactEmail, @notes)
                  RETURNING id",
                new { name, shortCode, contactEmail, notes },
                transaction: tx,
                cancellationToken: ct));

        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO club_admins (club_id, user_id, granted_by)
                  VALUES (@id, @firstAdminUserId, @grantedBy)",
                new { id, firstAdminUserId, grantedBy },
                transaction: tx,
                cancellationToken: ct));

        tx.Commit();
        return id;
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        string name,
        string shortCode,
        string contactEmail,
        string? notes,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE clubs
                  SET name = @name,
                      short_code = @shortCode,
                      contact_email = @contactEmail,
                      notes = @notes,
                      updated_at = now()
                  WHERE id = @id",
                new { id, name, shortCode, contactEmail, notes },
                cancellationToken: ct));
        return rows > 0;
    }
}
```

- [ ] **Step 4: Add seeder helper**

In `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`, after `CreateLeagueAsync`, append:

```csharp
public async Task<Guid> CreateClubAsync(
    string name,
    string shortCode,
    string contactEmail = "club@example.com",
    string? notes = null)
{
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    return await conn.ExecuteScalarAsync<Guid>(
        @"INSERT INTO clubs (name, short_code, contact_email, notes)
          VALUES (@name, @shortCode, @contactEmail, @notes)
          RETURNING id",
        new { name, shortCode, contactEmail, notes });
}
```

- [ ] **Step 5: Write repo tests**

`tests/smash-dates.IntegrationTests/Repositories/ClubRepositoryTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClubRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private ClubRepository _repo = null!;

    public ClubRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _seeder = new TestDataSeeder(fixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.ResetAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _fixture.ConnectionString,
            })
            .Build();
        _repo = new ClubRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateWithFirstAdminAsync_PersistsClubAndGrant()
    {
        var admin = await _seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var id = await _repo.CreateWithFirstAdminAsync(
            "Acme Badminton Club", "ACME", "contact@acme.test", "private notes",
            firstAdminUserId: admin.Id, grantedBy: admin.Id);

        var loaded = await _repo.GetByIdAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Acme Badminton Club");
        loaded.ShortCode.Should().Be("ACME");
        loaded.ContactEmail.Should().Be("contact@acme.test");
        loaded.Notes.Should().Be("private notes");
    }

    [Fact]
    public async Task ListAsync_ReturnsAllClubs_OrderedByName()
    {
        await _seeder.CreateClubAsync("Beta", "BETA");
        await _seeder.CreateClubAsync("Alpha", "ALPHA");

        var results = await _repo.ListAsync();

        results.Select(c => c.Name).Should().ContainInOrder("Alpha", "Beta");
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var id = await _seeder.CreateClubAsync("Acme", "ACME");

        var updated = await _repo.UpdateAsync(id, "Acme Renamed", "ACMER", "new@acme.test", "fresh notes");

        updated.Should().BeTrue();
        var loaded = await _repo.GetByIdAsync(id);
        loaded!.Name.Should().Be("Acme Renamed");
        loaded.ShortCode.Should().Be("ACMER");
        loaded.ContactEmail.Should().Be("new@acme.test");
        loaded.Notes.Should().Be("fresh notes");
    }

    [Fact]
    public async Task CreateWithFirstAdminAsync_DuplicateShortCode_Throws()
    {
        var admin = await _seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        await _seeder.CreateClubAsync("First", "ACME");

        var act = () => _repo.CreateWithFirstAdminAsync(
            "Second", "ACME", "x@y.test", null, admin.Id, admin.Id);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
```

- [ ] **Step 6: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*ClubRepositoryTests"
```

Expect: 4/4 pass.

```
git add Models/Club.cs Repositories/IClubRepository.cs Repositories/ClubRepository.cs tests/smash-dates.IntegrationTests/Repositories/ClubRepositoryTests.cs tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs
git commit -m "feat(clubs): add Club model and Dapper repository"
```

---

### Task 3: ClubAdmin grant model, repository, authoriser

**Files:**
- Create: `Models/ClubAdminGrant.cs`
- Create: `Repositories/IClubAdminRepository.cs`
- Create: `Repositories/ClubAdminRepository.cs`
- Create: `Services/Auth/ClubAuthorizer.cs`
- Create: `tests/smash-dates.IntegrationTests/Repositories/ClubAdminRepositoryTests.cs`
- Modify: `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`

- [ ] **Step 1: Write the model**

`Models/ClubAdminGrant.cs`:

```csharp
namespace smash_dates.Models;

public sealed class ClubAdminGrant
{
    public Guid ClubId { get; init; }
    public Guid UserId { get; init; }
    public DateTime GrantedAt { get; init; }
    public Guid? GrantedBy { get; init; }
}
```

- [ ] **Step 2: Write the interface**

`Repositories/IClubAdminRepository.cs`:

```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IClubAdminRepository
{
    Task<bool> IsAdminAsync(Guid clubId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ClubAdminGrant>> ListByClubAsync(Guid clubId, CancellationToken ct = default);
    Task GrantAsync(Guid clubId, Guid userId, Guid? grantedBy, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid clubId, Guid userId, CancellationToken ct = default);
    Task<RevokeResult> RevokeUnlessLastAsync(Guid clubId, Guid userId, CancellationToken ct = default);
}
```

`RevokeResult` enum is already defined in `Repositories/ILeagueAdminRepository.cs` (slice 2a). Reuse it — both repos share the same outcome semantics. Do **not** re-declare it.

- [ ] **Step 3: Write the repository**

`Repositories/ClubAdminRepository.cs`:

```csharp
using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class ClubAdminRepository : IClubAdminRepository
{
    private const string SelectColumns = "club_id, user_id, granted_at, granted_by";

    private readonly IDbConnectionFactory _factory;

    public ClubAdminRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> IsAdminAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT EXISTS(SELECT 1 FROM club_admins
                                WHERE club_id = @clubId AND user_id = @userId)",
                new { clubId, userId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ClubAdminGrant>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<ClubAdminGrant>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM club_admins WHERE club_id = @clubId ORDER BY granted_at",
                new { clubId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task GrantAsync(Guid clubId, Guid userId, Guid? grantedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO club_admins (club_id, user_id, granted_by)
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
                "DELETE FROM club_admins WHERE club_id = @clubId AND user_id = @userId",
                new { clubId, userId },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<RevokeResult> RevokeUnlessLastAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var outcome = await conn.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                @"WITH existing AS (
                      SELECT 1 FROM club_admins
                      WHERE club_id = @clubId AND user_id = @userId
                  ),
                  attempt AS (
                      DELETE FROM club_admins
                      WHERE club_id = @clubId
                        AND user_id = @userId
                        AND (SELECT COUNT(*) FROM club_admins WHERE club_id = @clubId) > 1
                      RETURNING 1
                  )
                  SELECT CASE
                      WHEN NOT EXISTS (SELECT 1 FROM existing) THEN 'NotAdmin'
                      WHEN EXISTS (SELECT 1 FROM attempt) THEN 'Revoked'
                      ELSE 'WouldBeLastAdmin'
                  END",
                new { clubId, userId },
                cancellationToken: ct));

        return outcome switch
        {
            "Revoked" => RevokeResult.Revoked,
            "WouldBeLastAdmin" => RevokeResult.WouldBeLastAdmin,
            _ => RevokeResult.NotAdmin,
        };
    }
}
```

- [ ] **Step 4: Write `ClubAuthorizer`**

`Services/Auth/ClubAuthorizer.cs`:

```csharp
using System.Security.Claims;
using smash_dates.Repositories;

namespace smash_dates.Services.Auth;

/// Inline authorisation helper: a request is permitted if the caller is SystemAdmin
/// or holds a ClubAdmin grant for the specific club. Returns null on success or a
/// 401/403 IResult to short-circuit the endpoint.
public static class ClubAuthorizer
{
    public static async Task<IResult?> RequireClubAdminAsync(
        ClaimsPrincipal principal,
        Guid clubId,
        IClubAdminRepository admins,
        CancellationToken ct)
    {
        var userId = principal.UserId();
        if (userId is null) return Results.Unauthorized();

        if (principal.IsSystemAdmin()) return null;

        var isAdmin = await admins.IsAdminAsync(clubId, userId.Value, ct);
        return isAdmin ? null : Results.Forbid();
    }
}
```

- [ ] **Step 5: Add seeder helper**

In `TestDataSeeder.cs`, after `CreateClubAsync`, append:

```csharp
public async Task GrantClubAdminAsync(Guid clubId, Guid userId, Guid? grantedBy = null)
{
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await conn.ExecuteAsync(
        @"INSERT INTO club_admins (club_id, user_id, granted_by)
          VALUES (@clubId, @userId, @grantedBy)
          ON CONFLICT DO NOTHING",
        new { clubId, userId, grantedBy });
}
```

- [ ] **Step 6: Write repo tests (mirroring `LeagueAdminRepositoryTests`)**

`tests/smash-dates.IntegrationTests/Repositories/ClubAdminRepositoryTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClubAdminRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private ClubAdminRepository _repo = null!;

    public ClubAdminRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _seeder = new TestDataSeeder(fixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.ResetAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _fixture.ConnectionString,
            })
            .Build();
        _repo = new ClubAdminRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GrantAsync_PersistsAndIsIdempotent()
    {
        var user = await _seeder.CreateUserAsync("u@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");

        await _repo.GrantAsync(clubId, user.Id, user.Id);
        await _repo.GrantAsync(clubId, user.Id, user.Id);

        (await _repo.IsAdminAsync(clubId, user.Id)).Should().BeTrue();
        (await _repo.ListByClubAsync(clubId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task RevokeUnlessLastAsync_RemovesNonLast_ReturnsRevoked()
    {
        var sole = await _seeder.CreateUserAsync("a@example.com", "correct-horse-battery");
        var second = await _seeder.CreateUserAsync("b@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        await _seeder.GrantClubAdminAsync(clubId, sole.Id, sole.Id);
        await _seeder.GrantClubAdminAsync(clubId, second.Id, sole.Id);

        var outcome = await _repo.RevokeUnlessLastAsync(clubId, second.Id);

        outcome.Should().Be(RevokeResult.Revoked);
    }

    [Fact]
    public async Task RevokeUnlessLastAsync_LastAdmin_ReturnsWouldBeLast()
    {
        var sole = await _seeder.CreateUserAsync("a@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        await _seeder.GrantClubAdminAsync(clubId, sole.Id, sole.Id);

        var outcome = await _repo.RevokeUnlessLastAsync(clubId, sole.Id);

        outcome.Should().Be(RevokeResult.WouldBeLastAdmin);
        (await _repo.IsAdminAsync(clubId, sole.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task RevokeUnlessLastAsync_NotAGrant_ReturnsNotAdmin()
    {
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");

        var outcome = await _repo.RevokeUnlessLastAsync(clubId, Guid.NewGuid());

        outcome.Should().Be(RevokeResult.NotAdmin);
    }

    [Fact]
    public async Task RevokeAsync_ForcedDelete_AlwaysRemovesIfPresent()
    {
        var sole = await _seeder.CreateUserAsync("a@example.com", "correct-horse-battery");
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        await _seeder.GrantClubAdminAsync(clubId, sole.Id, sole.Id);

        var removed = await _repo.RevokeAsync(clubId, sole.Id);

        removed.Should().BeTrue();
        (await _repo.IsAdminAsync(clubId, sole.Id)).Should().BeFalse();
    }
}
```

- [ ] **Step 7: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*ClubAdminRepositoryTests"
```

Expect: 5/5 pass.

```
git add Models/ClubAdminGrant.cs Repositories/IClubAdminRepository.cs Repositories/ClubAdminRepository.cs Services/Auth/ClubAuthorizer.cs tests/smash-dates.IntegrationTests/Repositories/ClubAdminRepositoryTests.cs tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs
git commit -m "feat(clubs): add ClubAdmin grant model, repository, and authoriser"
```

---

### Task 4: `ClubLeagueMembership` model + repository

**Files:**
- Create: `Models/ClubLeagueMembership.cs`
- Create: `Models/MembershipStatus.cs`
- Create: `Repositories/IClubLeagueMembershipRepository.cs`
- Create: `Repositories/ClubLeagueMembershipRepository.cs`
- Create: `tests/smash-dates.IntegrationTests/Repositories/ClubLeagueMembershipRepositoryTests.cs`
- Modify: `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`

- [ ] **Step 1: Write the enum**

`Models/MembershipStatus.cs`:

```csharp
namespace smash_dates.Models;

public enum MembershipStatus
{
    Pending,
    Accepted,
    Declined,
    Withdrawn,
    Expelled,
}
```

- [ ] **Step 2: Write the model**

`Models/ClubLeagueMembership.cs`:

```csharp
namespace smash_dates.Models;

public sealed class ClubLeagueMembership
{
    public Guid Id { get; init; }
    public Guid ClubId { get; init; }
    public Guid LeagueId { get; init; }
    public MembershipStatus Status { get; init; }
    public DateTime InvitedAt { get; init; }
    public Guid? InvitedBy { get; init; }
    public DateTime? RespondedAt { get; init; }
    public Guid? RespondedBy { get; init; }
}
```

- [ ] **Step 3: Write the interface**

`Repositories/IClubLeagueMembershipRepository.cs`:

```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IClubLeagueMembershipRepository
{
    Task<ClubLeagueMembership?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ClubLeagueMembership>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<IReadOnlyList<ClubLeagueMembership>> ListByClubAsync(Guid clubId, CancellationToken ct = default);

    /// Creates a Pending row. Throws PostgresException SQLSTATE 23505 if a non-terminal
    /// (Pending or Accepted) membership already exists for (club, league).
    Task<Guid> InviteAsync(Guid clubId, Guid leagueId, Guid invitedBy, CancellationToken ct = default);

    /// Transitions Pending → newStatus (Accepted | Declined). Returns false if the row
    /// wasn't Pending. Used for accept/decline.
    Task<bool> TransitionFromPendingAsync(
        Guid membershipId,
        MembershipStatus newStatus,
        Guid respondedBy,
        CancellationToken ct = default);

    /// Transitions Accepted → newStatus (Withdrawn | Expelled). Returns false if the
    /// row wasn't Accepted. Used for withdraw/expel.
    Task<bool> TransitionFromAcceptedAsync(
        Guid membershipId,
        MembershipStatus newStatus,
        Guid respondedBy,
        CancellationToken ct = default);
}
```

- [ ] **Step 4: Write the repository**

`Repositories/ClubLeagueMembershipRepository.cs`:

```csharp
using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class ClubLeagueMembershipRepository : IClubLeagueMembershipRepository
{
    private const string SelectColumns =
        "id, club_id, league_id, status, invited_at, invited_by, responded_at, responded_by";

    private readonly IDbConnectionFactory _factory;

    public ClubLeagueMembershipRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<ClubLeagueMembership?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM club_league_memberships WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return row?.ToModel();
    }

    public async Task<IReadOnlyList<ClubLeagueMembership>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM club_league_memberships WHERE league_id = @leagueId ORDER BY invited_at",
                new { leagueId },
                cancellationToken: ct));
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<IReadOnlyList<ClubLeagueMembership>> ListByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<Row>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM club_league_memberships WHERE club_id = @clubId ORDER BY invited_at",
                new { clubId },
                cancellationToken: ct));
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<Guid> InviteAsync(Guid clubId, Guid leagueId, Guid invitedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO club_league_memberships (club_id, league_id, status, invited_by)
                  VALUES (@clubId, @leagueId, 'Pending', @invitedBy)
                  RETURNING id",
                new { clubId, leagueId, invitedBy },
                cancellationToken: ct));
    }

    public async Task<bool> TransitionFromPendingAsync(
        Guid membershipId,
        MembershipStatus newStatus,
        Guid respondedBy,
        CancellationToken ct = default)
    {
        if (newStatus is not (MembershipStatus.Accepted or MembershipStatus.Declined))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), "Only Accepted or Declined valid here.");
        }

        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE club_league_memberships
                  SET status = @newStatus,
                      responded_at = now(),
                      responded_by = @respondedBy
                  WHERE id = @membershipId AND status = 'Pending'",
                new { membershipId, newStatus = newStatus.ToString(), respondedBy },
                cancellationToken: ct));
        return rows > 0;
    }

    public async Task<bool> TransitionFromAcceptedAsync(
        Guid membershipId,
        MembershipStatus newStatus,
        Guid respondedBy,
        CancellationToken ct = default)
    {
        if (newStatus is not (MembershipStatus.Withdrawn or MembershipStatus.Expelled))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), "Only Withdrawn or Expelled valid here.");
        }

        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                @"UPDATE club_league_memberships
                  SET status = @newStatus,
                      responded_at = now(),
                      responded_by = @respondedBy
                  WHERE id = @membershipId AND status = 'Accepted'",
                new { membershipId, newStatus = newStatus.ToString(), respondedBy },
                cancellationToken: ct));
        return rows > 0;
    }

    private sealed class Row
    {
        public Guid Id { get; init; }
        public Guid ClubId { get; init; }
        public Guid LeagueId { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTime InvitedAt { get; init; }
        public Guid? InvitedBy { get; init; }
        public DateTime? RespondedAt { get; init; }
        public Guid? RespondedBy { get; init; }

        public ClubLeagueMembership ToModel() => new()
        {
            Id = Id,
            ClubId = ClubId,
            LeagueId = LeagueId,
            Status = Enum.Parse<MembershipStatus>(Status),
            InvitedAt = InvitedAt,
            InvitedBy = InvitedBy,
            RespondedAt = RespondedAt,
            RespondedBy = RespondedBy,
        };
    }
}
```

- [ ] **Step 5: Add seeder helper**

In `TestDataSeeder.cs`, append:

```csharp
public async Task<Guid> CreateMembershipAsync(
    Guid clubId,
    Guid leagueId,
    MembershipStatus status = MembershipStatus.Pending,
    Guid? invitedBy = null)
{
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    return await conn.ExecuteScalarAsync<Guid>(
        @"INSERT INTO club_league_memberships (club_id, league_id, status, invited_by)
          VALUES (@clubId, @leagueId, @status, @invitedBy)
          RETURNING id",
        new { clubId, leagueId, status = status.ToString(), invitedBy });
}
```

- [ ] **Step 6: Write repo tests**

`tests/smash-dates.IntegrationTests/Repositories/ClubLeagueMembershipRepositoryTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClubLeagueMembershipRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private ClubLeagueMembershipRepository _repo = null!;

    public ClubLeagueMembershipRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _seeder = new TestDataSeeder(fixture.ConnectionString);
    }

    public async ValueTask InitializeAsync()
    {
        await _fixture.ResetAsync();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _fixture.ConnectionString,
            })
            .Build();
        _repo = new ClubLeagueMembershipRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task InviteAsync_PersistsPendingRow()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");

        var id = await _repo.InviteAsync(clubId, leagueId, sys.Id);

        var loaded = await _repo.GetByIdAsync(id);
        loaded!.Status.Should().Be(MembershipStatus.Pending);
        loaded.InvitedBy.Should().Be(sys.Id);
    }

    [Fact]
    public async Task InviteAsync_DuplicateActive_Throws()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        await _repo.InviteAsync(clubId, leagueId, sys.Id);

        var act = () => _repo.InviteAsync(clubId, leagueId, sys.Id);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }

    [Fact]
    public async Task InviteAsync_PreviousTerminalAllowsReinvite()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        await _seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Declined, sys.Id);

        var newId = await _repo.InviteAsync(clubId, leagueId, sys.Id);

        (await _repo.GetByIdAsync(newId))!.Status.Should().Be(MembershipStatus.Pending);
    }

    [Fact]
    public async Task TransitionFromPendingAsync_AcceptsPending()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        var id = await _repo.InviteAsync(clubId, leagueId, sys.Id);

        var ok = await _repo.TransitionFromPendingAsync(id, MembershipStatus.Accepted, sys.Id);

        ok.Should().BeTrue();
        (await _repo.GetByIdAsync(id))!.Status.Should().Be(MembershipStatus.Accepted);
    }

    [Fact]
    public async Task TransitionFromPendingAsync_RefusesIfNotPending()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        var id = await _seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Declined, sys.Id);

        var ok = await _repo.TransitionFromPendingAsync(id, MembershipStatus.Accepted, sys.Id);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task TransitionFromAcceptedAsync_WithdrawsAccepted()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await _seeder.CreateClubAsync("Acme", "ACME");
        var id = await _seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);

        var ok = await _repo.TransitionFromAcceptedAsync(id, MembershipStatus.Withdrawn, sys.Id);

        ok.Should().BeTrue();
        (await _repo.GetByIdAsync(id))!.Status.Should().Be(MembershipStatus.Withdrawn);
    }

    [Fact]
    public async Task ListByLeagueAsync_ReturnsAllStatuses()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        var clubA = await _seeder.CreateClubAsync("A", "AAA");
        var clubB = await _seeder.CreateClubAsync("B", "BBB");
        await _seeder.CreateMembershipAsync(clubA, leagueId, MembershipStatus.Accepted, sys.Id);
        await _seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Pending, sys.Id);

        var results = await _repo.ListByLeagueAsync(leagueId);

        results.Select(r => r.Status).Should().BeEquivalentTo(
            new[] { MembershipStatus.Accepted, MembershipStatus.Pending });
    }
}
```

- [ ] **Step 7: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*ClubLeagueMembership*"
```

Expect: 7/7 pass.

```
git add Models/ClubLeagueMembership.cs Models/MembershipStatus.cs Repositories/IClubLeagueMembershipRepository.cs Repositories/ClubLeagueMembershipRepository.cs tests/smash-dates.IntegrationTests/Repositories/ClubLeagueMembershipRepositoryTests.cs tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs
git commit -m "feat(memberships): add ClubLeagueMembership model and repository"
```

---

### Task 5: Club endpoints (POST/GET/GET-by-id/PATCH)

**Files:**
- Create: `Endpoints/Clubs/ClubEndpoints.cs`
- Create: `Endpoints/Clubs/CreateClubEndpoint.cs`
- Create: `Endpoints/Clubs/ListClubsEndpoint.cs`
- Create: `Endpoints/Clubs/GetClubEndpoint.cs`
- Create: `Endpoints/Clubs/UpdateClubEndpoint.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/{Create,List,Get,Update}ClubEndpointTests.cs` (4 files)
- Modify: `Program.cs`

- [ ] **Step 1: Group registration**

`Endpoints/Clubs/ClubEndpoints.cs`:

```csharp
namespace smash_dates.Endpoints.Clubs;

public static class ClubEndpoints
{
    public static IEndpointRouteBuilder MapClubEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs")
            .RequireAuthorization();

        group.MapCreateClubEndpoint();
        group.MapListClubsEndpoint();
        group.MapGetClubEndpoint();
        group.MapUpdateClubEndpoint();
        return app;
    }
}
```

- [ ] **Step 2: Create endpoint (SystemAdmin only, atomic + first-admin)**

`Endpoints/Clubs/CreateClubEndpoint.cs`:

```csharp
using System.Net.Mail;
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Clubs;

public static class CreateClubEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxNotesLength = 4000;
    private const string DuplicateSqlState = "23505";
    private const string CheckViolationSqlState = "23514";
    private const string ForeignKeyViolationSqlState = "23503";

    public sealed record CreateClubRequest(
        string Name,
        string ShortCode,
        string ContactEmail,
        string? Notes,
        Guid FirstClubAdminUserId);

    public sealed record ClubResponse(Guid Id, string Name, string ShortCode, string ContactEmail, string? Notes);

    public static IEndpointRouteBuilder MapCreateClubEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle)
            .RequireAuthorization(AuthorizationPolicies.SystemAdmin);
        return app;
    }

    private static async Task<IResult> Handle(
        CreateClubRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        CancellationToken ct)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        var shortCode = (request.ShortCode ?? string.Empty).Trim().ToUpperInvariant();
        if (shortCode.Length < 3 || shortCode.Length > 5)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "ShortCode must be 3–5 characters");
        if (!System.Text.RegularExpressions.Regex.IsMatch(shortCode, "^[A-Z0-9]+$"))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "ShortCode must be ASCII letters/digits");

        var email = (request.ContactEmail ?? string.Empty).Trim();
        if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid contact email");

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim();
        if (notes is { Length: > MaxNotesLength })
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Notes too long");

        if (request.FirstClubAdminUserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "FirstClubAdminUserId is required");

        var grantedBy = principal.UserId()
            ?? throw new InvalidOperationException("Authenticated principal missing user id.");

        try
        {
            var id = await clubs.CreateWithFirstAdminAsync(
                name, shortCode, email, notes, request.FirstClubAdminUserId, grantedBy, ct);
            return Results.Created($"/api/clubs/{id}", new ClubResponse(id, name, shortCode, email, notes));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Name or ShortCode already in use");
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolationSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "FirstClubAdminUserId references unknown user");
        }
        catch (PostgresException ex) when (ex.SqlState == CheckViolationSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "ShortCode failed schema constraint");
        }
    }
}
```

- [ ] **Step 3: List + Get + Update endpoints**

`Endpoints/Clubs/ListClubsEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Clubs;

public static class ListClubsEndpoint
{
    public sealed record ClubSummary(Guid Id, string Name, string ShortCode, string ContactEmail, string? Notes);

    public static IEndpointRouteBuilder MapListClubsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(IClubRepository clubs, CancellationToken ct)
    {
        var rows = await clubs.ListAsync(ct);
        return Results.Ok(rows.Select(c => new ClubSummary(c.Id, c.Name, c.ShortCode, c.ContactEmail, c.Notes)).ToArray());
    }
}
```

`Endpoints/Clubs/GetClubEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Clubs;

public static class GetClubEndpoint
{
    public sealed record ClubDetail(Guid Id, string Name, string ShortCode, string ContactEmail, string? Notes);

    public static IEndpointRouteBuilder MapGetClubEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid id, IClubRepository clubs, CancellationToken ct)
    {
        var club = await clubs.GetByIdAsync(id, ct);
        return club is null
            ? Results.NotFound()
            : Results.Ok(new ClubDetail(club.Id, club.Name, club.ShortCode, club.ContactEmail, club.Notes));
    }
}
```

`Endpoints/Clubs/UpdateClubEndpoint.cs`:

```csharp
using System.Net.Mail;
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Clubs;

public static class UpdateClubEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxNotesLength = 4000;
    private const string DuplicateSqlState = "23505";
    private const string CheckViolationSqlState = "23514";

    public sealed record UpdateClubRequest(string Name, string ShortCode, string ContactEmail, string? Notes);

    public static IEndpointRouteBuilder MapUpdateClubEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid id,
        UpdateClubRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(id, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, id, clubAdmins, ct);
        if (authz is not null) return authz;

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        var shortCode = (request.ShortCode ?? string.Empty).Trim().ToUpperInvariant();
        if (shortCode.Length < 3 || shortCode.Length > 5
            || !System.Text.RegularExpressions.Regex.IsMatch(shortCode, "^[A-Z0-9]+$"))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid ShortCode");

        var email = (request.ContactEmail ?? string.Empty).Trim();
        if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid contact email");

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes!.Trim();
        if (notes is { Length: > MaxNotesLength })
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Notes too long");

        try
        {
            await clubs.UpdateAsync(id, name, shortCode, email, notes, ct);
            return Results.NoContent();
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Name or ShortCode already in use");
        }
        catch (PostgresException ex) when (ex.SqlState == CheckViolationSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "ShortCode failed schema constraint");
        }
    }
}
```

- [ ] **Step 4: Register in `Program.cs`**

Add `using smash_dates.Endpoints.Clubs;` and add after the existing repo registrations:

```csharp
builder.Services.AddScoped<IClubRepository, ClubRepository>();
builder.Services.AddScoped<IClubAdminRepository, ClubAdminRepository>();
builder.Services.AddScoped<IClubLeagueMembershipRepository, ClubLeagueMembershipRepository>();
```

And after `app.MapUserEndpoints();`:

```csharp
app.MapClubEndpoints();
```

- [ ] **Step 5: Write tests**

`tests/smash-dates.IntegrationTests/Endpoints/CreateClubEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateClubEndpointTests : IntegrationTestBase
{
    public CreateClubEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsSystemAdmin_CreatesClub_Returns201()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync("/api/clubs", new
        {
            name = "Acme",
            shortCode = "ACME",
            contactEmail = "contact@acme.test",
            notes = "founded 2020",
            firstClubAdminUserId = admin.Id,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.PostAsJsonAsync("/api/clubs", new
        {
            name = "X", shortCode = "XYZ", contactEmail = "x@y.test", firstClubAdminUserId = Guid.NewGuid(),
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_DuplicateShortCode_Returns409()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Seeder.CreateClubAsync("First", "ACME");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync("/api/clubs", new
        {
            name = "Second", shortCode = "ACME", contactEmail = "x@y.test", firstClubAdminUserId = admin.Id,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_InvalidShortCode_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync("/api/clubs", new
        {
            name = "Acme", shortCode = "AC", contactEmail = "x@y.test", firstClubAdminUserId = admin.Id,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_FirstAdminUnknown_Returns400()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/clubs", new
        {
            name = "Acme", shortCode = "ACME", contactEmail = "x@y.test", firstClubAdminUserId = Guid.NewGuid(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

`tests/smash-dates.IntegrationTests/Endpoints/ListClubsEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Clubs;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListClubsEndpointTests : IntegrationTestBase
{
    public ListClubsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/clubs");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_ReturnsAll_SortedByName()
    {
        await Seeder.CreateClubAsync("Beta", "BETA");
        await Seeder.CreateClubAsync("Alpha", "ALPHA");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync("/api/clubs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListClubsEndpoint.ClubSummary[]>();
        body!.Select(c => c.Name).Should().ContainInOrder("Alpha", "Beta");
    }
}
```

`tests/smash-dates.IntegrationTests/Endpoints/GetClubEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Clubs;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class GetClubEndpointTests : IntegrationTestBase
{
    public GetClubEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Existing_ReturnsClub()
    {
        var id = await Seeder.CreateClubAsync("Acme", "ACME", "contact@acme.test", "notes here");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/clubs/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetClubEndpoint.ClubDetail>();
        body!.ShortCode.Should().Be("ACME");
        body.ContactEmail.Should().Be("contact@acme.test");
    }

    [Fact]
    public async Task Get_Missing_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/clubs/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

`tests/smash-dates.IntegrationTests/Endpoints/UpdateClubEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class UpdateClubEndpointTests : IntegrationTestBase
{
    public UpdateClubEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private static HttpRequestMessage Patch(string url, object body)
    {
        return new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = JsonContent.Create(body),
        };
    }

    [Fact]
    public async Task Patch_AsClubAdmin_Updates_Returns204()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}", new
        {
            name = "Acme Renamed",
            shortCode = "ACMER",
            contactEmail = "contact@acme.test",
            notes = (string?)null,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Patch_AsNonAdmin_Returns403()
    {
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.SendAsync(Patch($"/api/clubs/{clubId}", new
        {
            name = "X", shortCode = "XYZ", contactEmail = "x@y.test", notes = (string?)null,
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 6: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*ClubEndpoint*"
```

Expect: 5 + 2 + 2 + 2 = 11 pass (across the four files).

```
git add Endpoints/Clubs Program.cs tests/smash-dates.IntegrationTests/Endpoints/CreateClubEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/ListClubsEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/GetClubEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/UpdateClubEndpointTests.cs
git commit -m "feat(clubs): add create/list/get/update club endpoints"
```

---

### Task 6: ClubAdmin grant endpoints

**Files:**
- Create: `Endpoints/ClubAdmins/ClubAdminEndpoints.cs`
- Create: `Endpoints/ClubAdmins/ListClubAdminsEndpoint.cs`
- Create: `Endpoints/ClubAdmins/GrantClubAdminEndpoint.cs`
- Create: `Endpoints/ClubAdmins/RevokeClubAdminEndpoint.cs`
- Create: 3 test files mirroring slice 2a's LeagueAdmin endpoint tests
- Modify: `Program.cs`

- [ ] **Step 1: Write the group and endpoints**

`Endpoints/ClubAdmins/ClubAdminEndpoints.cs`:

```csharp
namespace smash_dates.Endpoints.ClubAdmins;

public static class ClubAdminEndpoints
{
    public static IEndpointRouteBuilder MapClubAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/admins")
            .RequireAuthorization();

        group.MapListClubAdminsEndpoint();
        group.MapGrantClubAdminEndpoint();
        group.MapRevokeClubAdminEndpoint();
        return app;
    }
}
```

`Endpoints/ClubAdmins/ListClubAdminsEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.ClubAdmins;

public static class ListClubAdminsEndpoint
{
    public sealed record ClubAdminSummary(Guid UserId, string Email, string? DisplayName, DateTime GrantedAt);

    public static IEndpointRouteBuilder MapListClubAdminsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        IClubRepository clubs,
        IClubAdminRepository admins,
        IUserRepository users,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var grants = await admins.ListByClubAsync(clubId, ct);
        var summaries = new List<ClubAdminSummary>(grants.Count);
        foreach (var grant in grants)
        {
            var user = await users.GetByIdAsync(grant.UserId, ct);
            if (user is null) continue;
            summaries.Add(new ClubAdminSummary(user.Id, user.Email, user.DisplayName, grant.GrantedAt));
        }
        return Results.Ok(summaries);
    }
}
```

`Endpoints/ClubAdmins/GrantClubAdminEndpoint.cs`:

```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.ClubAdmins;

public static class GrantClubAdminEndpoint
{
    public sealed record GrantRequest(Guid UserId);

    public static IEndpointRouteBuilder MapGrantClubAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        GrantRequest request,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IClubAdminRepository admins,
        IUserRepository users,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;

        if (request.UserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "userId is required");

        if (await users.GetByIdAsync(request.UserId, ct) is null)
            return Results.NotFound();

        var grantedBy = principal.UserId()!.Value;
        await admins.GrantAsync(clubId, request.UserId, grantedBy, ct);
        return Results.NoContent();
    }
}
```

`Endpoints/ClubAdmins/RevokeClubAdminEndpoint.cs`:

```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.ClubAdmins;

public static class RevokeClubAdminEndpoint
{
    public static IEndpointRouteBuilder MapRevokeClubAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{userId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        Guid userId,
        ClaimsPrincipal principal,
        IClubRepository clubs,
        IClubAdminRepository admins,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, clubId, admins, ct);
        if (authz is not null) return authz;

        if (principal.IsSystemAdmin())
        {
            var removed = await admins.RevokeAsync(clubId, userId, ct);
            return removed ? Results.NoContent() : Results.NotFound();
        }

        var outcome = await admins.RevokeUnlessLastAsync(clubId, userId, ct);
        return outcome switch
        {
            RevokeResult.Revoked => Results.NoContent(),
            RevokeResult.NotAdmin => Results.NotFound(),
            RevokeResult.WouldBeLastAdmin => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Cannot remove the last ClubAdmin",
                detail: "Grant ClubAdmin to another user first, or ask a SystemAdmin to force the removal."),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}
```

- [ ] **Step 2: Wire into Program.cs**

After `app.MapClubEndpoints();`:

```csharp
app.MapClubAdminEndpoints();
```

Add `using smash_dates.Endpoints.ClubAdmins;`.

- [ ] **Step 3: Write tests (mirroring slice 2a's three LeagueAdmin endpoint test files)**

Three new test files, each almost line-for-line a copy of the LeagueAdmin tests in `tests/smash-dates.IntegrationTests/Endpoints/`, substituting `clubs` for `leagues` and using `Seeder.CreateClubAsync` / `Seeder.GrantClubAdminAsync`. The total should be: 3 tests for List + 5 for Grant + 5 for Revoke = 13 tests.

The test files are named `ListClubAdminsEndpointTests.cs`, `GrantClubAdminEndpointTests.cs`, `RevokeClubAdminEndpointTests.cs`. Each uses the same patterns as its league counterpart — the implementer should re-read the slice-2a files for shape but adapt:
- Seed a SystemAdmin user (for SystemAdmin paths) or a plain user + ClubAdmin grant.
- Login that user.
- Hit `/api/clubs/{clubId}/admins[/{userId}]`.
- Assert the 401/403/404/409/204 cases as in the LeagueAdmin tests.

Reference for the implementer: look at `tests/smash-dates.IntegrationTests/Endpoints/ListLeagueAdminsEndpointTests.cs`, `GrantLeagueAdminEndpointTests.cs`, `RevokeLeagueAdminEndpointTests.cs`. Replace:
- `CreateSystemAdminUserAsync` + `CreateLeagueAsync` → `CreateSystemAdminUserAsync` + `CreateClubAsync` (note: `CreateClubAsync` needs `("Acme", "ACME")` args).
- `GrantLeagueAdminAsync(leagueId, ...)` → `GrantClubAdminAsync(clubId, ...)`.
- Route: `/api/leagues/{id}/admins` → `/api/clubs/{id}/admins`.
- Test class names: `ListLeagueAdmins...` → `ListClubAdmins...` etc.

- [ ] **Step 4: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*ClubAdmins*"
```

Expect: 13 pass.

```
git add Endpoints/ClubAdmins Program.cs tests/smash-dates.IntegrationTests/Endpoints/ListClubAdminsEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/GrantClubAdminEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/RevokeClubAdminEndpointTests.cs
git commit -m "feat(club-admins): list/grant/revoke endpoints with last-admin invariant"
```

---

### Task 7: Membership endpoints — invite + list (both directions)

**Files:**
- Create: `Endpoints/Memberships/MembershipEndpoints.cs`
- Create: `Endpoints/Memberships/InviteMembershipEndpoint.cs`
- Create: `Endpoints/Memberships/ListLeagueMembershipsEndpoint.cs`
- Create: `Endpoints/Memberships/ListClubMembershipsEndpoint.cs`
- Create: stub files for accept/decline/withdraw/expel (filled in Tasks 8 + 9)
- Create: 3 test files
- Modify: `Program.cs`

- [ ] **Step 1: Write the group**

`Endpoints/Memberships/MembershipEndpoints.cs`:

```csharp
namespace smash_dates.Endpoints.Memberships;

public static class MembershipEndpoints
{
    public static IEndpointRouteBuilder MapMembershipEndpoints(this IEndpointRouteBuilder app)
    {
        // League-scoped: invite + list-by-league + state transitions
        var league = app.MapGroup("/api/leagues/{leagueId:guid}/memberships")
            .RequireAuthorization();
        league.MapInviteMembershipEndpoint();
        league.MapListLeagueMembershipsEndpoint();
        league.MapAcceptMembershipEndpoint();
        league.MapDeclineMembershipEndpoint();
        league.MapWithdrawMembershipEndpoint();
        league.MapExpelMembershipEndpoint();

        // Club-scoped: list-by-club only
        var club = app.MapGroup("/api/clubs/{clubId:guid}/memberships")
            .RequireAuthorization();
        club.MapListClubMembershipsEndpoint();
        return app;
    }
}
```

- [ ] **Step 2: Invite endpoint**

`Endpoints/Memberships/InviteMembershipEndpoint.cs`:

```csharp
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Memberships;

public static class InviteMembershipEndpoint
{
    private const string DuplicateSqlState = "23505";
    private const string ForeignKeySqlState = "23503";

    public sealed record InviteRequest(Guid ClubId);
    public sealed record InviteResponse(Guid Id, Guid ClubId, Guid LeagueId, string Status);

    public static IEndpointRouteBuilder MapInviteMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        InviteRequest request,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository leagueAdmins,
        IClubRepository clubs,
        IClubLeagueMembershipRepository memberships,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        if (request.ClubId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "clubId is required");
        if (await clubs.GetByIdAsync(request.ClubId, ct) is null)
            return Results.NotFound();

        var invitedBy = principal.UserId()!.Value;

        try
        {
            var id = await memberships.InviteAsync(request.ClubId, leagueId, invitedBy, ct);
            return Results.Created(
                $"/api/leagues/{leagueId}/memberships/{id}",
                new InviteResponse(id, request.ClubId, leagueId, "Pending"));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateSqlState)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Active membership already exists",
                detail: "There is already a Pending or Accepted membership for this club in this league.");
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeySqlState)
        {
            return Results.NotFound();
        }
    }
}
```

- [ ] **Step 3: List-by-league endpoint**

`Endpoints/Memberships/ListLeagueMembershipsEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Memberships;

public static class ListLeagueMembershipsEndpoint
{
    public sealed record MembershipSummary(
        Guid Id,
        Guid ClubId,
        Guid LeagueId,
        string Status,
        DateTime InvitedAt,
        DateTime? RespondedAt);

    public static IEndpointRouteBuilder MapListLeagueMembershipsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        ILeagueRepository leagues,
        IClubLeagueMembershipRepository memberships,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var rows = await memberships.ListByLeagueAsync(leagueId, ct);
        return Results.Ok(rows.Select(m => new MembershipSummary(
            m.Id, m.ClubId, m.LeagueId, m.Status.ToString(), m.InvitedAt, m.RespondedAt)).ToArray());
    }
}
```

- [ ] **Step 4: List-by-club endpoint**

`Endpoints/Memberships/ListClubMembershipsEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Memberships;

public static class ListClubMembershipsEndpoint
{
    public sealed record ClubMembershipSummary(
        Guid Id,
        Guid ClubId,
        Guid LeagueId,
        string Status,
        DateTime InvitedAt,
        DateTime? RespondedAt);

    public static IEndpointRouteBuilder MapListClubMembershipsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid clubId,
        IClubRepository clubs,
        IClubLeagueMembershipRepository memberships,
        CancellationToken ct)
    {
        if (await clubs.GetByIdAsync(clubId, ct) is null) return Results.NotFound();

        var rows = await memberships.ListByClubAsync(clubId, ct);
        return Results.Ok(rows.Select(m => new ClubMembershipSummary(
            m.Id, m.ClubId, m.LeagueId, m.Status.ToString(), m.InvitedAt, m.RespondedAt)).ToArray());
    }
}
```

- [ ] **Step 5: Stub the 4 transition endpoints so the build compiles**

Create these four files containing just the `Map*` extension method (empty body returning the builder). They are filled in Tasks 8 + 9.

`Endpoints/Memberships/AcceptMembershipEndpoint.cs`:

```csharp
namespace smash_dates.Endpoints.Memberships;

public static class AcceptMembershipEndpoint
{
    public static IEndpointRouteBuilder MapAcceptMembershipEndpoint(this IEndpointRouteBuilder app) => app;
}
```

`Endpoints/Memberships/DeclineMembershipEndpoint.cs`:

```csharp
namespace smash_dates.Endpoints.Memberships;

public static class DeclineMembershipEndpoint
{
    public static IEndpointRouteBuilder MapDeclineMembershipEndpoint(this IEndpointRouteBuilder app) => app;
}
```

`Endpoints/Memberships/WithdrawMembershipEndpoint.cs`:

```csharp
namespace smash_dates.Endpoints.Memberships;

public static class WithdrawMembershipEndpoint
{
    public static IEndpointRouteBuilder MapWithdrawMembershipEndpoint(this IEndpointRouteBuilder app) => app;
}
```

`Endpoints/Memberships/ExpelMembershipEndpoint.cs`:

```csharp
namespace smash_dates.Endpoints.Memberships;

public static class ExpelMembershipEndpoint
{
    public static IEndpointRouteBuilder MapExpelMembershipEndpoint(this IEndpointRouteBuilder app) => app;
}
```

- [ ] **Step 6: Wire into Program.cs**

Add `using smash_dates.Endpoints.Memberships;` and after `app.MapClubAdminEndpoints();`:

```csharp
app.MapMembershipEndpoints();
```

- [ ] **Step 7: Write tests**

`tests/smash-dates.IntegrationTests/Endpoints/InviteMembershipEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class InviteMembershipEndpointTests : IntegrationTestBase
{
    public InviteMembershipEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsLeagueAdmin_CreatesPending_Returns201()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/memberships", new { clubId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/memberships", new { clubId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_DuplicateActive_Returns409()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateMembershipAsync(clubId, leagueId, smash_dates.Models.MembershipStatus.Pending, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/memberships", new { clubId });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_UnknownClub_Returns404()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/memberships", new { clubId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

`tests/smash-dates.IntegrationTests/Endpoints/ListLeagueMembershipsEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Memberships;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListLeagueMembershipsEndpointTests : IntegrationTestBase
{
    public ListLeagueMembershipsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsMemberships()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubA = await Seeder.CreateClubAsync("A", "AAA");
        var clubB = await Seeder.CreateClubAsync("B", "BBB");
        await Seeder.CreateMembershipAsync(clubA, leagueId, MembershipStatus.Accepted, sys.Id);
        await Seeder.CreateMembershipAsync(clubB, leagueId, MembershipStatus.Pending, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListLeagueMembershipsEndpoint.MembershipSummary[]>();
        body!.Select(m => m.Status).Should().BeEquivalentTo(new[] { "Accepted", "Pending" });
    }

    [Fact]
    public async Task Get_UnknownLeague_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}/memberships");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

`tests/smash-dates.IntegrationTests/Endpoints/ListClubMembershipsEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Memberships;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListClubMembershipsEndpointTests : IntegrationTestBase
{
    public ListClubMembershipsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsMembershipsForClub()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueA = await Seeder.CreateLeagueAsync("A", sys.Id);
        var leagueB = await Seeder.CreateLeagueAsync("B", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.CreateMembershipAsync(clubId, leagueA, MembershipStatus.Accepted, sys.Id);
        await Seeder.CreateMembershipAsync(clubId, leagueB, MembershipStatus.Pending, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/clubs/{clubId}/memberships");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListClubMembershipsEndpoint.ClubMembershipSummary[]>();
        body!.Should().HaveCount(2);
    }
}
```

- [ ] **Step 8: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*Membership*"
```

Expect: 4 + 2 + 1 = 7 pass.

```
git add Endpoints/Memberships Program.cs tests/smash-dates.IntegrationTests/Endpoints/InviteMembershipEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/ListLeagueMembershipsEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/ListClubMembershipsEndpointTests.cs
git commit -m "feat(memberships): invite + list endpoints"
```

---

### Task 8: Membership accept + decline

**Files:**
- Replace stubs: `Endpoints/Memberships/AcceptMembershipEndpoint.cs`, `DeclineMembershipEndpoint.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/AcceptMembershipEndpointTests.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/DeclineMembershipEndpointTests.cs`

- [ ] **Step 1: Replace accept stub**

`Endpoints/Memberships/AcceptMembershipEndpoint.cs`:

```csharp
using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Memberships;

public static class AcceptMembershipEndpoint
{
    public static IEndpointRouteBuilder MapAcceptMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{membershipId:guid}/accept", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        IClubLeagueMembershipRepository memberships,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var membership = await memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.LeagueId != leagueId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, membership.ClubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var userId = principal.UserId()!.Value;
        var ok = await memberships.TransitionFromPendingAsync(membershipId, MembershipStatus.Accepted, userId, ct);
        return ok
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: $"Membership is not Pending");
    }
}
```

- [ ] **Step 2: Replace decline stub**

`Endpoints/Memberships/DeclineMembershipEndpoint.cs` — same shape as accept but `MembershipStatus.Declined`. Copy the accept file, rename to `DeclineMembershipEndpoint` / `MapDeclineMembershipEndpoint`, route `/{membershipId:guid}/decline`, pass `MembershipStatus.Declined` to `TransitionFromPendingAsync`.

```csharp
using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Memberships;

public static class DeclineMembershipEndpoint
{
    public static IEndpointRouteBuilder MapDeclineMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{membershipId:guid}/decline", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        IClubLeagueMembershipRepository memberships,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var membership = await memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.LeagueId != leagueId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, membership.ClubId, clubAdmins, ct);
        if (authz is not null) return authz;

        var userId = principal.UserId()!.Value;
        var ok = await memberships.TransitionFromPendingAsync(membershipId, MembershipStatus.Declined, userId, ct);
        return ok
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Membership is not Pending");
    }
}
```

- [ ] **Step 3: Write tests**

`tests/smash-dates.IntegrationTests/Endpoints/AcceptMembershipEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class AcceptMembershipEndpointTests : IntegrationTestBase
{
    public AcceptMembershipEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsClubAdmin_AcceptsPending_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var clubAdmin = await Seeder.CreateUserAsync("ca@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, clubAdmin.Id, sys.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Pending, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "ca@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_NonClubAdmin_Returns403()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Pending, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_AlreadyAccepted_Returns409()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, sys.Id, sys.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_UnknownMembership_Returns404()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{Guid.NewGuid()}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

`tests/smash-dates.IntegrationTests/Endpoints/DeclineMembershipEndpointTests.cs`: mirror the accept tests with `/decline` route and one extra: confirm Accepted-state membership returns 409 on decline.

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class DeclineMembershipEndpointTests : IntegrationTestBase
{
    public DeclineMembershipEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsClubAdmin_DeclinesPending_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var clubAdmin = await Seeder.CreateUserAsync("ca@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, clubAdmin.Id, sys.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Pending, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "ca@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/decline", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_AcceptedMembership_Returns409()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, sys.Id, sys.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/decline", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
```

- [ ] **Step 4: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*AcceptMembership*"
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*DeclineMembership*"
```

Expect: 4 + 2 = 6 pass.

```
git add Endpoints/Memberships/AcceptMembershipEndpoint.cs Endpoints/Memberships/DeclineMembershipEndpoint.cs tests/smash-dates.IntegrationTests/Endpoints/AcceptMembershipEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/DeclineMembershipEndpointTests.cs
git commit -m "feat(memberships): accept + decline endpoints"
```

---

### Task 9: Membership withdraw + expel

**Files:**
- Replace stubs: `Endpoints/Memberships/WithdrawMembershipEndpoint.cs`, `ExpelMembershipEndpoint.cs`
- Create: 2 test files

- [ ] **Step 1: Withdraw endpoint** (ClubAdmin@thisClub, transitions Accepted → Withdrawn)

`Endpoints/Memberships/WithdrawMembershipEndpoint.cs`:

```csharp
using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Memberships;

public static class WithdrawMembershipEndpoint
{
    public static IEndpointRouteBuilder MapWithdrawMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{membershipId:guid}/withdraw", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        IClubLeagueMembershipRepository memberships,
        IClubAdminRepository clubAdmins,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var membership = await memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.LeagueId != leagueId) return Results.NotFound();

        var authz = await ClubAuthorizer.RequireClubAdminAsync(principal, membership.ClubId, clubAdmins, ct);
        if (authz is not null) return authz;

        // Mid-season block deferred to the slice that adds Season Entries.
        var userId = principal.UserId()!.Value;
        var ok = await memberships.TransitionFromAcceptedAsync(membershipId, MembershipStatus.Withdrawn, userId, ct);
        return ok
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Membership is not Accepted");
    }
}
```

- [ ] **Step 2: Expel endpoint** (LeagueAdmin@thisLeague | SystemAdmin, transitions Accepted → Expelled)

`Endpoints/Memberships/ExpelMembershipEndpoint.cs`:

```csharp
using System.Security.Claims;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Memberships;

public static class ExpelMembershipEndpoint
{
    public static IEndpointRouteBuilder MapExpelMembershipEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/{membershipId:guid}/expel", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid membershipId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository leagueAdmins,
        IClubLeagueMembershipRepository memberships,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null) return Results.NotFound();

        var membership = await memberships.GetByIdAsync(membershipId, ct);
        if (membership is null || membership.LeagueId != leagueId) return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
        if (authz is not null) return authz;

        // Mid-season block deferred to the slice that adds Season Entries.
        var userId = principal.UserId()!.Value;
        var ok = await memberships.TransitionFromAcceptedAsync(membershipId, MembershipStatus.Expelled, userId, ct);
        return ok
            ? Results.NoContent()
            : Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Membership is not Accepted");
    }
}
```

- [ ] **Step 3: Tests**

`tests/smash-dates.IntegrationTests/Endpoints/WithdrawMembershipEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class WithdrawMembershipEndpointTests : IntegrationTestBase
{
    public WithdrawMembershipEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsClubAdmin_WithdrawsAccepted_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var clubAdmin = await Seeder.CreateUserAsync("ca@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, clubAdmin.Id, sys.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "ca@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/withdraw", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_NotAccepted_Returns409()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, sys.Id, sys.Id);
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Pending, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/withdraw", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
```

`tests/smash-dates.IntegrationTests/Endpoints/ExpelMembershipEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ExpelMembershipEndpointTests : IntegrationTestBase
{
    public ExpelMembershipEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsLeagueAdmin_ExpelsAccepted_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/expel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_AsNonLeagueAdmin_Returns403()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        var membershipId = await Seeder.CreateMembershipAsync(clubId, leagueId, MembershipStatus.Accepted, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsync($"/api/leagues/{leagueId}/memberships/{membershipId}/expel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 4: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj
```

Expect: full backend suite green.

```
git add Endpoints/Memberships/WithdrawMembershipEndpoint.cs Endpoints/Memberships/ExpelMembershipEndpoint.cs tests/smash-dates.IntegrationTests/Endpoints/WithdrawMembershipEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/ExpelMembershipEndpointTests.cs
git commit -m "feat(memberships): withdraw + expel endpoints"
```

---

### Task 10: Angular ClubsApi + relax admin guard

**Files:**
- Create: `ClientApp/src/app/features/admin/clubs.api.ts`
- Modify: `ClientApp/src/app/features/admin/leagues.api.ts` (add membership methods)
- Modify: `ClientApp/src/app/app.routes.ts` (drop `systemAdminGuard` from `/admin`)

- [ ] **Step 1: New `clubs.api.ts`**

```typescript
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface ClubSummary {
  id: string;
  name: string;
  shortCode: string;
  contactEmail: string;
  notes: string | null;
}

export type ClubDetail = ClubSummary;

export interface ClubAdminSummary {
  userId: string;
  email: string;
  displayName: string | null;
  grantedAt: string;
}

export type MembershipStatus = 'Pending' | 'Accepted' | 'Declined' | 'Withdrawn' | 'Expelled';

export interface MembershipSummary {
  id: string;
  clubId: string;
  leagueId: string;
  status: MembershipStatus;
  invitedAt: string;
  respondedAt: string | null;
}

export interface CreateClubRequest {
  name: string;
  shortCode: string;
  contactEmail: string;
  notes: string | null;
  firstClubAdminUserId: string;
}

export interface UpdateClubRequest {
  name: string;
  shortCode: string;
  contactEmail: string;
  notes: string | null;
}

@Injectable({ providedIn: 'root' })
export class ClubsApi {
  private readonly http = inject(HttpClient);

  list(): Observable<ClubSummary[]> {
    return this.http.get<ClubSummary[]>('/api/clubs');
  }

  get(id: string): Observable<ClubDetail> {
    return this.http.get<ClubDetail>(`/api/clubs/${id}`);
  }

  create(req: CreateClubRequest): Observable<ClubSummary> {
    return this.http.post<ClubSummary>('/api/clubs', req);
  }

  update(id: string, req: UpdateClubRequest): Observable<void> {
    return this.http.patch<void>(`/api/clubs/${id}`, req);
  }

  listAdmins(clubId: string): Observable<ClubAdminSummary[]> {
    return this.http.get<ClubAdminSummary[]>(`/api/clubs/${clubId}/admins`);
  }

  grantAdmin(clubId: string, userId: string): Observable<void> {
    return this.http.post<void>(`/api/clubs/${clubId}/admins`, { userId });
  }

  revokeAdmin(clubId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`/api/clubs/${clubId}/admins/${userId}`);
  }

  listMemberships(clubId: string): Observable<MembershipSummary[]> {
    return this.http.get<MembershipSummary[]>(`/api/clubs/${clubId}/memberships`);
  }

  acceptMembership(leagueId: string, membershipId: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/memberships/${membershipId}/accept`, {});
  }

  declineMembership(leagueId: string, membershipId: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/memberships/${membershipId}/decline`, {});
  }

  withdrawMembership(leagueId: string, membershipId: string): Observable<void> {
    return this.http.post<void>(`/api/leagues/${leagueId}/memberships/${membershipId}/withdraw`, {});
  }
}
```

- [ ] **Step 2: Extend `leagues.api.ts` with invite/list-memberships/expel**

Append inside `LeaguesApi`:

```typescript
listMemberships(leagueId: string): Observable<MembershipSummary[]> {
  return this.http.get<MembershipSummary[]>(`/api/leagues/${leagueId}/memberships`);
}

invite(leagueId: string, clubId: string): Observable<void> {
  return this.http.post<void>(`/api/leagues/${leagueId}/memberships`, { clubId });
}

expel(leagueId: string, membershipId: string): Observable<void> {
  return this.http.post<void>(`/api/leagues/${leagueId}/memberships/${membershipId}/expel`, {});
}
```

Add the `MembershipSummary` type at the top of the file (or import from `clubs.api.ts` — but to avoid coupling, declare a local copy):

```typescript
export interface MembershipSummary {
  id: string;
  clubId: string;
  leagueId: string;
  status: 'Pending' | 'Accepted' | 'Declined' | 'Withdrawn' | 'Expelled';
  invitedAt: string;
  respondedAt: string | null;
}
```

- [ ] **Step 3: Loosen admin guard**

In `ClientApp/src/app/app.routes.ts`, change:

```typescript
{
  path: 'admin',
  canActivate: [authGuard, systemAdminGuard],
  loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
},
```

to:

```typescript
{
  path: 'admin',
  canActivate: [authGuard],
  loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
},
```

Remove the `import { systemAdminGuard } from './core/auth/system-admin.guard';` line at the top of the file. Per-action authz remains server-enforced.

- [ ] **Step 4: Build + tests + commit**

```
cd ClientApp
npm test
npm run build
cd ..
```

Both green.

```
git add ClientApp/src/app/features/admin/clubs.api.ts ClientApp/src/app/features/admin/leagues.api.ts ClientApp/src/app/app.routes.ts
git commit -m "feat(client): add ClubsApi, leagues membership methods, broaden admin guard"
```

---

### Task 11: Angular clubs list page

**Files:**
- Create: `ClientApp/src/app/features/admin/clubs-list.page.ts`
- Modify: `ClientApp/src/app/features/admin/admin.routes.ts`

- [ ] **Step 1: Write the page** (mirrors `leagues-list.page.ts` shape — first-admin email lookup before create)

`ClientApp/src/app/features/admin/clubs-list.page.ts`:

```typescript
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ClubsApi, ClubSummary } from './clubs.api';
import { LeaguesApi } from './leagues.api';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-clubs-list-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50">
      <header class="border-b border-slate-200 bg-white">
        <div class="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <span class="font-mono text-sm font-semibold tracking-wide text-slate-900">smash-dates / admin</span>
        </div>
      </header>

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <h1 class="font-mono text-2xl font-semibold text-slate-900">Clubs</h1>

        @if (canCreate()) {
          <form
            [formGroup]="form"
            (ngSubmit)="onCreate()"
            class="mt-6 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
          >
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Name</span>
              <input
                type="text"
                formControlName="name"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
                required
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Short code (3-5 chars)</span>
              <input
                type="text"
                formControlName="shortCode"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm uppercase focus:outline-none focus:ring-2 focus:ring-slate-900"
                required
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Contact email</span>
              <input
                type="email"
                formControlName="contactEmail"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
                required
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Notes</span>
              <input
                type="text"
                formControlName="notes"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              />
            </label>
            <label class="grid gap-1">
              <span class="font-mono text-xs uppercase tracking-wider text-slate-600">First admin email</span>
              <input
                type="email"
                formControlName="firstAdminEmail"
                class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
                required
              />
            </label>
            <button
              type="submit"
              [disabled]="submitting() || form.invalid"
              class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
            >
              {{ submitting() ? 'Creating…' : 'Create club' }}
            </button>
            @if (error()) {
              <p class="font-mono text-sm text-red-600" role="alert">{{ error() }}</p>
            }
          </form>
        }

        <ul class="mt-8 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (club of clubs(); track club.id) {
            <li class="px-4 py-3">
              <a
                [routerLink]="['/admin/clubs', club.id]"
                class="font-mono text-sm font-medium text-slate-900 hover:underline"
                >{{ club.shortCode }} · {{ club.name }}</a
              >
              <span class="ml-2 font-mono text-xs text-slate-500">{{ club.contactEmail }}</span>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No clubs yet.</li>
          }
        </ul>
      </main>
    </div>
  `,
})
export default class ClubsListPage {
  private readonly api = inject(ClubsApi);
  private readonly leagues = inject(LeaguesApi);
  private readonly auth = inject(AuthStore);

  protected readonly clubs = signal<ClubSummary[]>([]);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly canCreate = computed(() => this.auth.isSystemAdmin());

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    shortCode: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(5)] }),
    contactEmail: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    notes: new FormControl('', { nonNullable: true }),
    firstAdminEmail: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  constructor() {
    this.refresh();
  }

  private refresh(): void {
    this.api.list().subscribe({
      next: (rows) => this.clubs.set(rows),
      error: () => this.error.set('Failed to load clubs.'),
    });
  }

  protected onCreate(): void {
    const { name, shortCode, contactEmail, notes, firstAdminEmail } = this.form.getRawValue();
    this.submitting.set(true);
    this.error.set(null);

    this.leagues.lookupUser(firstAdminEmail.trim()).subscribe({
      next: (user) => {
        const trimmedNotes = notes.trim();
        this.api.create({
          name: name.trim(),
          shortCode: shortCode.trim().toUpperCase(),
          contactEmail: contactEmail.trim(),
          notes: trimmedNotes ? trimmedNotes : null,
          firstClubAdminUserId: user.id,
        }).subscribe({
          next: () => {
            this.submitting.set(false);
            this.form.reset({ name: '', shortCode: '', contactEmail: '', notes: '', firstAdminEmail: '' });
            this.refresh();
          },
          error: (err: { error?: { title?: string } }) => {
            this.submitting.set(false);
            this.error.set(err?.error?.title ?? 'Create failed.');
          },
        });
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('No registered user with that email.');
      },
    });
  }
}
```

- [ ] **Step 2: Register route**

In `admin.routes.ts`, append:

```typescript
{
  path: 'clubs',
  title: 'Clubs · smash-dates',
  loadComponent: () => import('./clubs-list.page'),
},
{
  path: 'clubs/:id',
  title: 'Club · smash-dates',
  loadComponent: () => import('./club-detail.page'),
},
```

- [ ] **Step 3: Build + commit**

```
cd ClientApp && npm run build && cd ..
```

(Build will fail until Task 12 adds `club-detail.page.ts`. Either add a stub first or do Tasks 11+12 together. Do them together — single commit.)

Defer commit to Task 12.

---

### Task 12: Angular club detail page (with admins + memberships)

**Files:**
- Create: `ClientApp/src/app/features/admin/club-detail.page.ts`

- [ ] **Step 1: Write the page**

`ClientApp/src/app/features/admin/club-detail.page.ts`:

```typescript
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import { ClubAdminSummary, ClubDetail, ClubsApi, MembershipSummary } from './clubs.api';
import { LeaguesApi } from './leagues.api';

@Component({
  selector: 'app-club-detail-page',
  imports: [ReactiveFormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-50">
      <header class="border-b border-slate-200 bg-white">
        <div class="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <span class="font-mono text-sm font-semibold tracking-wide text-slate-900">smash-dates / admin</span>
        </div>
      </header>

      <main class="mx-auto w-full max-w-5xl px-4 py-10">
        <a
          [routerLink]="['/admin/clubs']"
          class="font-mono text-xs uppercase tracking-wider text-slate-500 hover:underline"
          >← back to clubs</a
        >

        @if (club(); as c) {
          <h1 class="mt-2 font-mono text-2xl font-semibold text-slate-900">
            {{ c.shortCode }} · {{ c.name }}
          </h1>
          <p class="mt-1 font-mono text-sm text-slate-500">{{ c.contactEmail }}</p>
          @if (c.notes) {
            <p class="mt-1 font-mono text-sm text-slate-500">{{ c.notes }}</p>
          }
        }

        <h2 class="mt-8 font-mono text-lg font-semibold text-slate-900">Club admins</h2>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (admin of admins(); track admin.userId) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                {{ admin.displayName ?? admin.email }}
                <span class="ml-2 text-slate-500">{{ admin.email }}</span>
              </span>
              <button
                type="button"
                [attr.aria-label]="'Revoke ' + admin.email"
                (click)="onRevoke(admin.userId)"
                class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
              >
                Revoke
              </button>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No admins.</li>
          }
        </ul>

        <form
          [formGroup]="adminForm"
          (ngSubmit)="onGrant()"
          class="mt-4 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
        >
          <label class="grid gap-1">
            <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Add admin by email</span>
            <input
              type="email"
              formControlName="email"
              class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
              required
            />
          </label>
          <button
            type="submit"
            [disabled]="adminBusy() || adminForm.invalid"
            class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ adminBusy() ? 'Granting…' : 'Grant admin' }}
          </button>
          @if (adminError()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ adminError() }}</p>
          }
        </form>

        <h2 class="mt-10 font-mono text-lg font-semibold text-slate-900">League memberships</h2>
        <ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (m of memberships(); track m.id) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                league <span class="text-slate-500">{{ m.leagueId }}</span>
                <span class="ml-3 inline-block rounded bg-slate-200 px-2 py-0.5 text-xs">{{ m.status }}</span>
              </span>
              <div class="flex gap-2">
                @if (m.status === 'Pending') {
                  <button
                    type="button"
                    (click)="onAccept(m)"
                    class="rounded-md border border-emerald-300 px-3 py-1 text-xs text-emerald-700 hover:bg-emerald-50"
                  >Accept</button>
                  <button
                    type="button"
                    (click)="onDecline(m)"
                    class="rounded-md border border-amber-300 px-3 py-1 text-xs text-amber-700 hover:bg-amber-50"
                  >Decline</button>
                }
                @if (m.status === 'Accepted') {
                  <button
                    type="button"
                    (click)="onWithdraw(m)"
                    class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
                  >Withdraw</button>
                }
              </div>
            </li>
          } @empty {
            <li class="px-4 py-3 font-mono text-sm text-slate-500">No memberships.</li>
          }
        </ul>
      </main>
    </div>
  `,
})
export default class ClubDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ClubsApi);

  protected readonly clubId = signal('');
  protected readonly club = signal<ClubDetail | null>(null);
  protected readonly admins = signal<ClubAdminSummary[]>([]);
  protected readonly memberships = signal<MembershipSummary[]>([]);
  protected readonly adminBusy = signal(false);
  protected readonly adminError = signal<string | null>(null);

  protected readonly adminForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  constructor() {
    this.route.paramMap
      .pipe(
        tap((p) => this.clubId.set(p.get('id') ?? '')),
        switchMap((p) => this.api.get(p.get('id') ?? '')),
        tap((c) => this.club.set(c)),
      )
      .subscribe({
        next: () => {
          this.refreshAdmins();
          this.refreshMemberships();
        },
      });
  }

  private readonly leagues = inject(LeaguesApi);

  private refreshAdmins(): void {
    this.api.listAdmins(this.clubId()).subscribe({
      next: (rows) => this.admins.set(rows),
    });
  }

  private refreshMemberships(): void {
    this.api.listMemberships(this.clubId()).subscribe({
      next: (rows) => this.memberships.set(rows),
    });
  }

  protected onGrant(): void {
    const email = this.adminForm.getRawValue().email.trim();
    if (!email) return;
    this.adminBusy.set(true);
    this.adminError.set(null);
    this.leagues.lookupUser(email).subscribe({
      next: (user) => {
        this.api.grantAdmin(this.clubId(), user.id).subscribe({
          next: () => {
            this.adminBusy.set(false);
            this.adminForm.reset({ email: '' });
            this.refreshAdmins();
          },
          error: (err: { error?: { title?: string } }) => {
            this.adminBusy.set(false);
            this.adminError.set(err?.error?.title ?? 'Grant failed.');
          },
        });
      },
      error: () => {
        this.adminBusy.set(false);
        this.adminError.set('No registered user with that email.');
      },
    });
  }

  protected onRevoke(userId: string): void {
    this.api.revokeAdmin(this.clubId(), userId).subscribe({
      next: () => this.refreshAdmins(),
    });
  }

  protected onAccept(m: MembershipSummary): void {
    this.api.acceptMembership(m.leagueId, m.id).subscribe({ next: () => this.refreshMemberships() });
  }

  protected onDecline(m: MembershipSummary): void {
    this.api.declineMembership(m.leagueId, m.id).subscribe({ next: () => this.refreshMemberships() });
  }

  protected onWithdraw(m: MembershipSummary): void {
    this.api.withdrawMembership(m.leagueId, m.id).subscribe({ next: () => this.refreshMemberships() });
  }
}
```

- [ ] **Step 2: Build + test + commit (combined Task 11 + 12)**

```
cd ClientApp && npm test && npm run build && cd ..
```

```
git add ClientApp/src/app/features/admin/clubs-list.page.ts ClientApp/src/app/features/admin/club-detail.page.ts ClientApp/src/app/features/admin/admin.routes.ts
git commit -m "feat(client): clubs list + detail pages with admins and memberships"
```

---

### Task 13: League-detail page — invite UI + memberships section

**Files:**
- Modify: `ClientApp/src/app/features/admin/league-detail.page.ts`

- [ ] **Step 1: Add memberships list + invite form**

In the existing `league-detail.page.ts`, after the divisions section in the template, add a new section. Update the component to load memberships and clubs (so the invite picker has options):

Append imports:

```typescript
import { ClubsApi, ClubSummary } from './clubs.api';
import { MembershipSummary } from './leagues.api';
```

Append component fields:

```typescript
private readonly clubsApi = inject(ClubsApi);
protected readonly memberships = signal<MembershipSummary[]>([]);
protected readonly availableClubs = signal<ClubSummary[]>([]);

protected readonly inviteForm = new FormGroup({
  clubId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
});
```

Append methods:

```typescript
private refreshMemberships(): void {
  this.api.listMemberships(this.leagueId).subscribe({
    next: (rows) => this.memberships.set(rows),
  });
}

private refreshAvailableClubs(): void {
  this.clubsApi.list().subscribe({
    next: (rows) => this.availableClubs.set(rows),
  });
}

protected onInvite(): void {
  const clubId = this.inviteForm.getRawValue().clubId;
  if (!clubId) return;
  this.api.invite(this.leagueId, clubId).subscribe({
    next: () => {
      this.inviteForm.reset({ clubId: '' });
      this.refreshMemberships();
    },
    error: (err: { error?: { title?: string } }) => this.error.set(err?.error?.title ?? 'Invite failed.'),
  });
}

protected onExpel(m: MembershipSummary): void {
  this.api.expel(this.leagueId, m.id).subscribe({
    next: () => this.refreshMemberships(),
  });
}
```

In the existing constructor's tap chain, after the existing `tap((l) => this.league.set(l))` add:

```typescript
tap(() => {
  this.refreshMemberships();
  this.refreshAvailableClubs();
}),
```

Add a template block after the divisions list (paste before the closing `</main>`):

```html
<h2 class="mt-10 font-mono text-lg font-semibold text-slate-900">Member clubs</h2>
<ul class="mt-3 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
  @for (m of memberships(); track m.id) {
    <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
      <span>
        club <span class="text-slate-500">{{ m.clubId }}</span>
        <span class="ml-3 inline-block rounded bg-slate-200 px-2 py-0.5 text-xs">{{ m.status }}</span>
      </span>
      @if (m.status === 'Accepted') {
        <button
          type="button"
          (click)="onExpel(m)"
          class="rounded-md border border-red-300 px-3 py-1 text-xs text-red-700 hover:bg-red-50"
        >Expel</button>
      }
    </li>
  } @empty {
    <li class="px-4 py-3 font-mono text-sm text-slate-500">No member clubs.</li>
  }
</ul>

<form
  [formGroup]="inviteForm"
  (ngSubmit)="onInvite()"
  class="mt-4 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
>
  <label class="grid gap-1">
    <span class="font-mono text-xs uppercase tracking-wider text-slate-600">Invite club</span>
    <select
      formControlName="clubId"
      class="rounded-md border border-slate-300 px-3 py-2 font-mono text-sm focus:outline-none focus:ring-2 focus:ring-slate-900"
    >
      <option value="">-- choose a club --</option>
      @for (c of availableClubs(); track c.id) {
        <option [value]="c.id">{{ c.shortCode }} · {{ c.name }}</option>
      }
    </select>
  </label>
  <button
    type="submit"
    [disabled]="inviteForm.invalid"
    class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
  >
    Send invite
  </button>
</form>
```

- [ ] **Step 2: Build + commit**

```
cd ClientApp && npm test && npm run build && cd ..
git add ClientApp/src/app/features/admin/league-detail.page.ts
git commit -m "feat(client): league detail shows memberships and invite UI"
```

---

### Task 14: README + final sweep

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Append slice 2b section**

After the existing "Slice 2a" section, add:

```markdown
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

Frontend additions: `/admin/clubs` (list + create when SystemAdmin), `/admin/clubs/:id` (detail with admin management + memberships). League detail gains a member-clubs section with invite + expel.

The mid-season Withdraw/Expel block (per CONTEXT.md) is **deferred** until Seasons + Season Entries land.
```

- [ ] **Step 2: Full sweep**

```
dotnet test
cd ClientApp && npm test && npm run build && cd ..
```

All green.

- [ ] **Step 3: Commit**

```
git add README.md
git commit -m "docs(readme): document slice 2b endpoints"
```

---

## Self-Review

**Spec coverage:**
- Club fields + open registry + atomic first-admin creation ✓ (Tasks 1, 2, 5)
- ClubAdmin multi-admin self-perpetuating + last-admin invariant ✓ (Tasks 3, 6)
- ClubLeagueMembership lifecycle (5 states, terminal re-invite, partial unique index for active rows) ✓ (Tasks 1, 4, 7, 8, 9)
- Endpoint set from grilling Q7 ✓
- User lookup endpoint reused from slice 2a ✓
- Angular UIs for both clubs and memberships ✓ (Tasks 11–13)
- Admin guard broadened to plain auth so ClubAdmins reach their page ✓ (Task 10)
- Mid-season block deferred with code seam in place ✓ (commented in Tasks 9, withdraw + expel)
- Documentation ✓ (Task 14)

**Placeholder scan:** none.

**Type consistency:**
- `RevokeResult` enum reused from slice 2a (declared once in `ILeagueAdminRepository.cs`, referenced from `IClubAdminRepository.cs` and both repositories).
- `MembershipStatus` enum used identically across model, repo, endpoints, and tests.
- Endpoint route templates uniformly use `"/"` (matching slice 2a's normalisation).
- All `Map*Endpoint` extension methods follow the same shape as the league-admin endpoints (group's `.RequireAuthorization()` + inline `*Authorizer` for action-level checks).
- `principal.UserId()` / `principal.IsSystemAdmin()` extensions reused from `Services/Auth/ClaimsPrincipalExtensions.cs`.
