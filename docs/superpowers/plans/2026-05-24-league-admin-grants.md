# Slice 2a — LeagueAdmin Grants + Retro Auth Changes

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add LeagueAdmin role-grants per League so a SystemAdmin can hand off day-to-day league management. Retro-fit the existing `POST /api/leagues` and `POST /api/leagues/{id}/divisions` endpoints so they require / accept the new role. Add an authenticated user-lookup endpoint that the granter UI uses to resolve email → userId.

**Architecture:**
- New `league_admins` table (composite PK `(league_id, user_id)`, with `granted_at` / `granted_by`). One row per grant; many grants per league.
- New repository `ILeagueAdminRepository` + Dapper impl. New endpoints under `Endpoints/LeagueAdmins/`.
- Authorization for `LeagueAdmin@<thisLeague> | SystemAdmin` is done **inline** in each endpoint via a small `LeagueAuthorizer.RequireLeagueAdminAsync(...)` helper, not via a policy + handler. The route-param-aware policy machinery is more ceremony than this codebase needs at this stage.
- `POST /api/leagues` becomes transactional: insert the League row + the first `(league_id, user_id)` admin row in one transaction so a League can never be created adminless.
- Last-admin invariant enforced in `DELETE /api/leagues/{id}/admins/{userId}`: reject 409 if the removal would leave zero admins, unless caller is `SystemAdmin` (who may force the adminless state for emergency recovery).

**Tech Stack:** .NET 10 minimal API · Dapper · Npgsql · PostgreSQL · DbUp · Angular 21 (signals, NgRx Signal Stores, OnPush) · xUnit v3 + Microsoft.Testing.Platform.

**Out of scope (later slices):**
- Clubs and ClubAdmin grants (slice 2b).
- Club ↔ League Membership lifecycle (slice 2b).
- Real email notifications.
- Antiforgery enforcement on the new mutating endpoints (PR-follow-up; deferred for consistency with existing endpoints which also don't enforce).
- Re-issuing the SystemAdmin claim mid-session.

**Branch:** Create from current `main` (the foundation-slice PR is open at #8; this slice should branch from `main` and rebase later if/when #8 merges, or be stacked on it — see Task 0).

---

## File Structure

**Created:**
- `Migrations/Scripts/0007_create_league_admins.sql` — table.
- `Models/LeagueAdminGrant.cs` — POCO (matches `league_admins` row).
- `Repositories/ILeagueAdminRepository.cs` / `LeagueAdminRepository.cs`.
- `Services/Auth/LeagueAuthorizer.cs` — `RequireLeagueAdminAsync` helper.
- `Services/Auth/ClaimsPrincipalExtensions.cs` — `UserId()` + `IsSystemAdmin()` helpers used by `LeagueAuthorizer` and elsewhere.
- `Endpoints/LeagueAdmins/LeagueAdminEndpoints.cs` — group registration.
- `Endpoints/LeagueAdmins/ListLeagueAdminsEndpoint.cs` — `GET /api/leagues/{id}/admins`.
- `Endpoints/LeagueAdmins/GrantLeagueAdminEndpoint.cs` — `POST /api/leagues/{id}/admins`.
- `Endpoints/LeagueAdmins/RevokeLeagueAdminEndpoint.cs` — `DELETE /api/leagues/{id}/admins/{userId}`.
- `Endpoints/Users/UserEndpoints.cs` — group.
- `Endpoints/Users/LookupUserEndpoint.cs` — `GET /api/users/lookup?email=...`.
- Tests under `tests/smash-dates.IntegrationTests/Endpoints/` and `tests/smash-dates.IntegrationTests/Repositories/`.
- `ClientApp/src/app/features/admin/leagues.api.ts` — extended with admin + lookup methods (modify existing).
- `ClientApp/src/app/features/admin/league-admins.page.ts` — manage UI.
- `ClientApp/src/app/features/admin/admin.routes.ts` — register the new route (modify existing).

**Modified:**
- `Endpoints/Leagues/CreateLeagueEndpoint.cs` — body adds `FirstLeagueAdminUserId`; insert grant atomically.
- `Endpoints/Divisions/CreateDivisionEndpoint.cs` — authz from `SystemAdmin` to `LeagueAdmin-or-SystemAdmin` (inline check).
- `Endpoints/Leagues/GetLeagueEndpoint.cs` — drop `CreatedBy` from response (PR follow-up #3).
- `Endpoints/Leagues/LeagueEndpoints.cs` — register the new admins group; convert route templates to `"/"` for consistency (PR follow-up #2).
- `Endpoints/Leagues/CreateLeagueEndpoint.cs` / `ListLeaguesEndpoint.cs` — route template style (PR follow-up #2).
- `Endpoints/Divisions/DivisionEndpoints.cs` / `CreateDivisionEndpoint.cs` / `ListDivisionsEndpoint.cs` — route template style (PR follow-up #2).
- `Program.cs` — register `ILeagueAdminRepository`, map new endpoint groups.
- `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs` — add `CreateLeagueAdminGrantAsync` helper.
- `tests/smash-dates.IntegrationTests/Endpoints/CreateLeagueEndpointTests.cs` / `CreateDivisionEndpointTests.cs` — adjust for the new body field and the new authz model.
- `ClientApp/src/app/features/admin/leagues-list.page.ts` — create-league form now needs first-admin email + lookup. (Default: the current user; allow overriding by typing another user's email.)
- `README.md` — document the new endpoints.

---

### Task 0: Branch off main

- [ ] **Step 1: Create + check out the new branch**

```
git checkout main
git pull
git checkout -b feature/league-admin-grants
```

Note: PR #8 (foundation slice) is still open against `main`. This branch starts from `main` (pre-#8). If #8 merges first, no rebase needed. If #8 stays open, this slice will need to be rebased onto its branch — handle that at PR-creation time, not here.

- [ ] **Step 2: Confirm baseline tests pass**

```
dotnet test
cd ClientApp && npm test && cd ..
```

Expect all green.

---

### Task 1: Create `league_admins` table migration

**Files:**
- Create: `Migrations/Scripts/0007_create_league_admins.sql`

- [ ] **Step 1: Write the migration**

```sql
CREATE TABLE league_admins (
    league_id   uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    user_id     uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    granted_at  timestamptz NOT NULL DEFAULT now(),
    granted_by  uuid NULL REFERENCES users(id) ON DELETE SET NULL,
    PRIMARY KEY (league_id, user_id)
);

CREATE INDEX ix_league_admins_user ON league_admins (user_id);
```

`ON DELETE RESTRICT` on `user_id` prevents accidentally deleting a User who still owns admin grants. `granted_by` becomes NULL if the granter is later removed — historical attribution without blocking cleanup.

- [ ] **Step 2: Run migrator tests**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*DbMigratorTests"
```

Expect: 2/2 pass.

- [ ] **Step 3: Commit**

```
git add Migrations/Scripts/0007_create_league_admins.sql
git commit -m "feat(db): add league_admins table"
```

---

### Task 2: `LeagueAdminGrant` model + repository

**Files:**
- Create: `Models/LeagueAdminGrant.cs`
- Create: `Repositories/ILeagueAdminRepository.cs`
- Create: `Repositories/LeagueAdminRepository.cs`
- Create: `tests/smash-dates.IntegrationTests/Repositories/LeagueAdminRepositoryTests.cs`
- Modify: `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`

- [ ] **Step 1: Write the model**

`Models/LeagueAdminGrant.cs`:

```csharp
namespace smash_dates.Models;

public sealed class LeagueAdminGrant
{
    public Guid LeagueId { get; init; }
    public Guid UserId { get; init; }
    public DateTime GrantedAt { get; init; }
    public Guid? GrantedBy { get; init; }
}
```

- [ ] **Step 2: Write the interface**

`Repositories/ILeagueAdminRepository.cs`:

```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ILeagueAdminRepository
{
    Task<bool> IsAdminAsync(Guid leagueId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<LeagueAdminGrant>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<int> CountByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task GrantAsync(Guid leagueId, Guid userId, Guid? grantedBy, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid leagueId, Guid userId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Write the repository**

`Repositories/LeagueAdminRepository.cs`:

```csharp
using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class LeagueAdminRepository : ILeagueAdminRepository
{
    private const string SelectColumns = "league_id, user_id, granted_at, granted_by";

    private readonly IDbConnectionFactory _factory;

    public LeagueAdminRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> IsAdminAsync(Guid leagueId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                @"SELECT EXISTS(SELECT 1 FROM league_admins
                                WHERE league_id = @leagueId AND user_id = @userId)",
                new { leagueId, userId },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<LeagueAdminGrant>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<LeagueAdminGrant>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM league_admins WHERE league_id = @leagueId ORDER BY granted_at",
                new { leagueId },
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<int> CountByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM league_admins WHERE league_id = @leagueId",
                new { leagueId },
                cancellationToken: ct));
    }

    public async Task GrantAsync(Guid leagueId, Guid userId, Guid? grantedBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO league_admins (league_id, user_id, granted_by)
                  VALUES (@leagueId, @userId, @grantedBy)
                  ON CONFLICT (league_id, user_id) DO NOTHING",
                new { leagueId, userId, grantedBy },
                cancellationToken: ct));
    }

    public async Task<bool> RevokeAsync(Guid leagueId, Guid userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM league_admins WHERE league_id = @leagueId AND user_id = @userId",
                new { leagueId, userId },
                cancellationToken: ct));
        return rows > 0;
    }
}
```

`ON CONFLICT DO NOTHING` makes `GrantAsync` idempotent — re-granting an existing admin is a no-op rather than a 23505.

- [ ] **Step 4: Add seeder helper**

In `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`, append a method (just before the closing brace of the class):

```csharp
public async Task GrantLeagueAdminAsync(Guid leagueId, Guid userId, Guid? grantedBy = null)
{
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    await conn.ExecuteAsync(
        @"INSERT INTO league_admins (league_id, user_id, granted_by)
          VALUES (@leagueId, @userId, @grantedBy)
          ON CONFLICT DO NOTHING",
        new { leagueId, userId, grantedBy });
}
```

- [ ] **Step 5: Write failing repo tests**

`tests/smash-dates.IntegrationTests/Repositories/LeagueAdminRepositoryTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using smash_dates.Data;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

[Collection(IntegrationTestCollection.Name)]
public sealed class LeagueAdminRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly TestDataSeeder _seeder;
    private LeagueAdminRepository _repo = null!;

    public LeagueAdminRepositoryTests(PostgresFixture fixture)
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
        _repo = new LeagueAdminRepository(new NpgsqlConnectionFactory(config));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GrantAsync_PersistsGrant()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);

        await _repo.GrantAsync(leagueId, sys.Id, grantedBy: sys.Id);

        (await _repo.IsAdminAsync(leagueId, sys.Id)).Should().BeTrue();
        (await _repo.CountByLeagueAsync(leagueId)).Should().Be(1);
    }

    [Fact]
    public async Task GrantAsync_DuplicateIsIdempotent()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);

        await _repo.GrantAsync(leagueId, sys.Id, sys.Id);
        await _repo.GrantAsync(leagueId, sys.Id, sys.Id);

        (await _repo.CountByLeagueAsync(leagueId)).Should().Be(1);
    }

    [Fact]
    public async Task RevokeAsync_RemovesGrant_AndReturnsTrue()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);
        await _repo.GrantAsync(leagueId, sys.Id, sys.Id);

        var revoked = await _repo.RevokeAsync(leagueId, sys.Id);

        revoked.Should().BeTrue();
        (await _repo.IsAdminAsync(leagueId, sys.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_NonExistent_ReturnsFalse()
    {
        var sys = await _seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await _seeder.CreateLeagueAsync("NL", sys.Id);

        var revoked = await _repo.RevokeAsync(leagueId, Guid.NewGuid());

        revoked.Should().BeFalse();
    }
}
```

- [ ] **Step 6: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*LeagueAdminRepositoryTests"
```

Expect: 4/4 pass.

```
git add Models/LeagueAdminGrant.cs Repositories/ILeagueAdminRepository.cs Repositories/LeagueAdminRepository.cs tests/smash-dates.IntegrationTests/Repositories/LeagueAdminRepositoryTests.cs tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs
git commit -m "feat(auth): add LeagueAdminGrant model and repository"
```

---

### Task 3: `ClaimsPrincipal` extensions and `LeagueAuthorizer` helper

**Files:**
- Create: `Services/Auth/ClaimsPrincipalExtensions.cs`
- Create: `Services/Auth/LeagueAuthorizer.cs`

- [ ] **Step 1: Write `ClaimsPrincipalExtensions`**

`Services/Auth/ClaimsPrincipalExtensions.cs`:

```csharp
using System.Security.Claims;

namespace smash_dates.Services.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid? UserId(this ClaimsPrincipal principal)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idClaim, out var id) ? id : null;
    }

    public static bool IsSystemAdmin(this ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(AuthorizationPolicies.SystemAdminClaim) == "true";
    }
}
```

- [ ] **Step 2: Write `LeagueAuthorizer`**

`Services/Auth/LeagueAuthorizer.cs`:

```csharp
using System.Security.Claims;
using smash_dates.Repositories;

namespace smash_dates.Services.Auth;

/// <summary>
/// Inline authorization helper: a request is permitted if the caller is SystemAdmin
/// OR holds a LeagueAdmin grant for the specific league referenced in the route.
/// Returns null on success, or a 401/403 IResult to short-circuit the endpoint.
/// </summary>
public static class LeagueAuthorizer
{
    public static async Task<IResult?> RequireLeagueAdminAsync(
        ClaimsPrincipal principal,
        Guid leagueId,
        ILeagueAdminRepository admins,
        CancellationToken ct)
    {
        var userId = principal.UserId();
        if (userId is null) return Results.Unauthorized();

        if (principal.IsSystemAdmin()) return null;

        var isAdmin = await admins.IsAdminAsync(leagueId, userId.Value, ct);
        return isAdmin ? null : Results.Forbid();
    }
}
```

- [ ] **Step 3: Commit (no test yet — exercised via endpoint tests in Task 5/6/7/8)**

```
git add Services/Auth/ClaimsPrincipalExtensions.cs Services/Auth/LeagueAuthorizer.cs
git commit -m "feat(auth): add LeagueAuthorizer helper and ClaimsPrincipal extensions"
```

---

### Task 4: Make `POST /api/leagues` atomic with first-admin grant

**Files:**
- Modify: `Endpoints/Leagues/CreateLeagueEndpoint.cs`
- Modify: `Repositories/ILeagueRepository.cs`
- Modify: `Repositories/LeagueRepository.cs`
- Modify: `tests/smash-dates.IntegrationTests/Endpoints/CreateLeagueEndpointTests.cs`
- Modify: `Program.cs` — register `ILeagueAdminRepository`.

- [ ] **Step 1: Add a transactional `CreateAsync` overload on `ILeagueRepository`**

`Repositories/ILeagueRepository.cs` — add a new method (do not remove the existing one yet; mark it `[Obsolete]` for the next slice cleanup):

```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ILeagueRepository
{
    Task<League?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<League>> ListAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(string name, string? description, Guid createdBy, CancellationToken ct = default);

    // Creates the league row and the initial LeagueAdmin grant in a single transaction.
    Task<Guid> CreateWithFirstAdminAsync(
        string name,
        string? description,
        Guid createdBy,
        Guid firstAdminUserId,
        CancellationToken ct = default);
}
```

- [ ] **Step 2: Implement the new method in `LeagueRepository`**

Append to `Repositories/LeagueRepository.cs`:

```csharp
    public async Task<Guid> CreateWithFirstAdminAsync(
        string name,
        string? description,
        Guid createdBy,
        Guid firstAdminUserId,
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
                @"INSERT INTO leagues (name, description, created_by)
                  VALUES (@name, @description, @createdBy)
                  RETURNING id",
                new { name, description, createdBy },
                transaction: tx,
                cancellationToken: ct));

        await conn.ExecuteAsync(
            new CommandDefinition(
                @"INSERT INTO league_admins (league_id, user_id, granted_by)
                  VALUES (@id, @firstAdminUserId, @createdBy)",
                new { id, firstAdminUserId, createdBy },
                transaction: tx,
                cancellationToken: ct));

        tx.Commit();
        return id;
    }
```

Add `using Npgsql;` if not already imported.

- [ ] **Step 3: Register `ILeagueAdminRepository` in Program.cs**

Open `Program.cs`. After the existing `builder.Services.AddScoped<IDivisionRepository, DivisionRepository>();`, add:

```csharp
builder.Services.AddScoped<ILeagueAdminRepository, LeagueAdminRepository>();
```

- [ ] **Step 4: Update `CreateLeagueEndpoint` to use the new flow**

Replace the body of `Endpoints/Leagues/CreateLeagueEndpoint.cs` with:

```csharp
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Leagues;

public static class CreateLeagueEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const string DuplicateNameSqlState = "23505";
    private const string ForeignKeyViolationSqlState = "23503";

    public sealed record CreateLeagueRequest(string Name, string? Description, Guid FirstLeagueAdminUserId);
    public sealed record LeagueResponse(Guid Id, string Name, string? Description);

    public static IEndpointRouteBuilder MapCreateLeagueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle)
            .RequireAuthorization(AuthorizationPolicies.SystemAdmin);
        return app;
    }

    private static async Task<IResult> Handle(
        CreateLeagueRequest request,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        CancellationToken ct)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim();
        if (description is { Length: > MaxDescriptionLength })
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Description too long");

        if (request.FirstLeagueAdminUserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "FirstLeagueAdminUserId is required");

        var createdBy = principal.UserId()
            ?? throw new InvalidOperationException("Authenticated principal missing user id.");

        try
        {
            var id = await leagues.CreateWithFirstAdminAsync(name, description, createdBy, request.FirstLeagueAdminUserId, ct);
            return Results.Created($"/api/leagues/{id}", new LeagueResponse(id, name, description));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateNameSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "League name already in use");
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolationSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "FirstLeagueAdminUserId references unknown user");
        }
    }
}
```

- [ ] **Step 5: Update tests**

Edit `tests/smash-dates.IntegrationTests/Endpoints/CreateLeagueEndpointTests.cs`. Every successful `PostAsJsonAsync("/api/leagues", new { name = ... })` call must now also include `firstLeagueAdminUserId`. The admin user already exists at this point in each test — use `admin.Id`.

Add two new tests:

```csharp
[Fact]
public async Task Post_FirstAdminUserUnknown_Returns400()
{
    await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

    var response = await Client.PostAsJsonAsync("/api/leagues", new
    {
        name = "North London",
        firstLeagueAdminUserId = Guid.NewGuid(),
    });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}

[Fact]
public async Task Post_CreatesInitialAdminGrant()
{
    var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
    await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

    var response = await Client.PostAsJsonAsync("/api/leagues", new
    {
        name = "North London",
        firstLeagueAdminUserId = admin.Id,
    });
    response.StatusCode.Should().Be(HttpStatusCode.Created);

    // Use the seeder's connection to verify the grant landed.
    await using var conn = new Npgsql.NpgsqlConnection(Fixture.ConnectionString);
    await conn.OpenAsync();
    var count = await Dapper.SqlMapper.ExecuteScalarAsync<int>(
        conn,
        "SELECT count(*) FROM league_admins WHERE user_id = @id",
        new { id = admin.Id });
    count.Should().Be(1);
}
```

In each existing test that currently uses `new { name = ..., description = ... }`, you may need to either: (a) construct the SystemAdmin first via `Seeder.CreateSystemAdminUserAsync` and then pass `firstLeagueAdminUserId = admin.Id`, or (b) for the negative tests that fail before the body is fully validated (e.g. 401 / empty-name), keep them as-is — they'll still fail with the same status.

Specifically:
- `Post_AsSystemAdmin_CreatesLeague_Returns201` — add `firstLeagueAdminUserId = <admin.Id>`.
- `Post_DuplicateName_Returns409` — both calls add the field with admin.Id.
- `Post_EmptyName_Returns400` — no change needed; the name validation fires first.
- `Post_Anonymous_Returns401` — no change needed.
- `Post_AsNonAdmin_Returns403` — no change needed.

- [ ] **Step 6: Run all integration tests**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj
```

Expect: all green.

- [ ] **Step 7: Commit**

```
git add Endpoints/Leagues/CreateLeagueEndpoint.cs Repositories/ILeagueRepository.cs Repositories/LeagueRepository.cs Program.cs tests/smash-dates.IntegrationTests/Endpoints/CreateLeagueEndpointTests.cs
git commit -m "feat(leagues): create league with atomic first admin grant"
```

---

### Task 5: `GET /api/leagues/{id}/admins` endpoint

**Files:**
- Create: `Endpoints/LeagueAdmins/LeagueAdminEndpoints.cs`
- Create: `Endpoints/LeagueAdmins/ListLeagueAdminsEndpoint.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/ListLeagueAdminsEndpointTests.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Write the group registration**

`Endpoints/LeagueAdmins/LeagueAdminEndpoints.cs`:

```csharp
namespace smash_dates.Endpoints.LeagueAdmins;

public static class LeagueAdminEndpoints
{
    public static IEndpointRouteBuilder MapLeagueAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues/{leagueId:guid}/admins")
            .RequireAuthorization();

        group.MapListLeagueAdminsEndpoint();
        group.MapGrantLeagueAdminEndpoint();
        group.MapRevokeLeagueAdminEndpoint();
        return app;
    }
}
```

- [ ] **Step 2: Write the list endpoint**

`Endpoints/LeagueAdmins/ListLeagueAdminsEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.LeagueAdmins;

public static class ListLeagueAdminsEndpoint
{
    public sealed record LeagueAdminSummary(Guid UserId, string Email, string? DisplayName, DateTime GrantedAt);

    public static IEndpointRouteBuilder MapListLeagueAdminsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        ILeagueRepository leagues,
        ILeagueAdminRepository admins,
        IUserRepository users,
        CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(leagueId, ct);
        if (league is null) return Results.NotFound();

        var grants = await admins.ListByLeagueAsync(leagueId, ct);
        var summaries = new List<LeagueAdminSummary>(grants.Count);
        foreach (var grant in grants)
        {
            var user = await users.GetByIdAsync(grant.UserId, ct);
            if (user is null) continue;
            summaries.Add(new LeagueAdminSummary(user.Id, user.Email, user.DisplayName, grant.GrantedAt));
        }
        return Results.Ok(summaries);
    }
}
```

(N+1 query is fine here — a League's admin list is bounded and small.)

- [ ] **Step 3: Wire endpoint group in Program.cs**

In `Program.cs`, add `using smash_dates.Endpoints.LeagueAdmins;` to the imports, and `app.MapLeagueAdminEndpoints();` after `app.MapDivisionEndpoints();`.

- [ ] **Step 4: Stub the grant and revoke endpoints to avoid compile failure**

The group registration in Step 1 calls `MapGrantLeagueAdminEndpoint` and `MapRevokeLeagueAdminEndpoint`. To keep the build green between this task and Task 6/7, create both files with a single placeholder mapping (404):

`Endpoints/LeagueAdmins/GrantLeagueAdminEndpoint.cs`:

```csharp
namespace smash_dates.Endpoints.LeagueAdmins;

public static class GrantLeagueAdminEndpoint
{
    public static IEndpointRouteBuilder MapGrantLeagueAdminEndpoint(this IEndpointRouteBuilder app)
    {
        // Implemented in Task 6.
        return app;
    }
}
```

`Endpoints/LeagueAdmins/RevokeLeagueAdminEndpoint.cs`:

```csharp
namespace smash_dates.Endpoints.LeagueAdmins;

public static class RevokeLeagueAdminEndpoint
{
    public static IEndpointRouteBuilder MapRevokeLeagueAdminEndpoint(this IEndpointRouteBuilder app)
    {
        // Implemented in Task 7.
        return app;
    }
}
```

These will be replaced wholesale in their respective tasks.

- [ ] **Step 5: Write tests**

`tests/smash-dates.IntegrationTests/Endpoints/ListLeagueAdminsEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.LeagueAdmins;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListLeagueAdminsEndpointTests : IntegrationTestBase
{
    public ListLeagueAdminsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}/admins");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_UnknownLeague_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}/admins");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ReturnsCurrentAdmins()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var second = await Seeder.CreateUserAsync("second@example.com", "correct-horse-battery", displayName: "Second");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, second.Id, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/admins");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListLeagueAdminsEndpoint.LeagueAdminSummary[]>();
        body!.Select(a => a.Email).Should().BeEquivalentTo(new[] { "sys@example.com", "second@example.com" });
        body!.Should().Contain(a => a.DisplayName == "Second");
    }
}
```

- [ ] **Step 6: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj
```

Expect: all green.

```
git add Endpoints/LeagueAdmins Program.cs tests/smash-dates.IntegrationTests/Endpoints/ListLeagueAdminsEndpointTests.cs
git commit -m "feat(league-admins): list endpoint and group registration"
```

---

### Task 6: `POST /api/leagues/{id}/admins` (grant)

**Files:**
- Replace: `Endpoints/LeagueAdmins/GrantLeagueAdminEndpoint.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/GrantLeagueAdminEndpointTests.cs`

- [ ] **Step 1: Replace the stub with the real endpoint**

`Endpoints/LeagueAdmins/GrantLeagueAdminEndpoint.cs`:

```csharp
using System.Security.Claims;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.LeagueAdmins;

public static class GrantLeagueAdminEndpoint
{
    private const string ForeignKeyViolationSqlState = "23503";

    public sealed record GrantRequest(Guid UserId);

    public static IEndpointRouteBuilder MapGrantLeagueAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        GrantRequest request,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository admins,
        IUserRepository users,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null)
            return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, admins, ct);
        if (authz is not null) return authz;

        if (request.UserId == Guid.Empty)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "userId is required");

        if (await users.GetByIdAsync(request.UserId, ct) is null)
            return Results.NotFound();

        var grantedBy = principal.UserId()!.Value;

        try
        {
            await admins.GrantAsync(leagueId, request.UserId, grantedBy, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == ForeignKeyViolationSqlState)
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }
}
```

- [ ] **Step 2: Write tests**

`tests/smash-dates.IntegrationTests/Endpoints/GrantLeagueAdminEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class GrantLeagueAdminEndpointTests : IntegrationTestBase
{
    public GrantLeagueAdminEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsLeagueAdmin_Grants_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var target = await Seeder.CreateUserAsync("target@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/admins", new { userId = target.Id });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/admins", new { userId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_UnknownLeague_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        var response = await Client.PostAsJsonAsync($"/api/leagues/{Guid.NewGuid()}/admins", new { userId = Guid.NewGuid() });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_UnknownUser_Returns404()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/admins", new { userId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_AlreadyAdmin_Idempotent_204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var first = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/admins", new { userId = sys.Id });
        var second = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/admins", new { userId = sys.Id });

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
```

- [ ] **Step 3: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter-class "*GrantLeagueAdmin*"
git add Endpoints/LeagueAdmins/GrantLeagueAdminEndpoint.cs tests/smash-dates.IntegrationTests/Endpoints/GrantLeagueAdminEndpointTests.cs
git commit -m "feat(league-admins): grant endpoint"
```

---

### Task 7: `DELETE /api/leagues/{id}/admins/{userId}` (revoke) + last-admin invariant

**Files:**
- Replace: `Endpoints/LeagueAdmins/RevokeLeagueAdminEndpoint.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/RevokeLeagueAdminEndpointTests.cs`

- [ ] **Step 1: Replace the stub**

`Endpoints/LeagueAdmins/RevokeLeagueAdminEndpoint.cs`:

```csharp
using System.Security.Claims;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.LeagueAdmins;

public static class RevokeLeagueAdminEndpoint
{
    public static IEndpointRouteBuilder MapRevokeLeagueAdminEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/{userId:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        Guid userId,
        ClaimsPrincipal principal,
        ILeagueRepository leagues,
        ILeagueAdminRepository admins,
        CancellationToken ct)
    {
        if (await leagues.GetByIdAsync(leagueId, ct) is null)
            return Results.NotFound();

        var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, admins, ct);
        if (authz is not null) return authz;

        var isAdmin = await admins.IsAdminAsync(leagueId, userId, ct);
        if (!isAdmin) return Results.NotFound();

        if (!principal.IsSystemAdmin())
        {
            var count = await admins.CountByLeagueAsync(leagueId, ct);
            if (count <= 1)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Cannot remove the last LeagueAdmin",
                    detail: "Grant LeagueAdmin to another user first, or ask a SystemAdmin to force the removal.");
            }
        }

        await admins.RevokeAsync(leagueId, userId, ct);
        return Results.NoContent();
    }
}
```

- [ ] **Step 2: Write tests**

`tests/smash-dates.IntegrationTests/Endpoints/RevokeLeagueAdminEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class RevokeLeagueAdminEndpointTests : IntegrationTestBase
{
    public RevokeLeagueAdminEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Delete_RemovesNonLastAdmin_Returns204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var second = await Seeder.CreateUserAsync("second@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, second.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{second.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_LastNonSystemAdmin_BlockedWith409()
    {
        // Non-SystemAdmin sole league admin tries to revoke themselves.
        var soleAdmin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        // A SystemAdmin must exist to create the league atomically; create one separately.
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, soleAdmin.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{soleAdmin.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Delete_LastAdmin_ForcedBySystemAdmin_204()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var soleAdmin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, soleAdmin.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{soleAdmin.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_NotAGrant_Returns404()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "sys@example.com", password = "correct-horse-battery" });

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_Returns403()
    {
        var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
        await Seeder.GrantLeagueAdminAsync(leagueId, sys.Id, sys.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.DeleteAsync($"/api/leagues/{leagueId}/admins/{sys.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 3: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj
git add Endpoints/LeagueAdmins/RevokeLeagueAdminEndpoint.cs tests/smash-dates.IntegrationTests/Endpoints/RevokeLeagueAdminEndpointTests.cs
git commit -m "feat(league-admins): revoke endpoint with last-admin invariant"
```

---

### Task 8: Switch `POST /api/leagues/{id}/divisions` authz to LeagueAdmin-or-SystemAdmin

**Files:**
- Modify: `Endpoints/Divisions/CreateDivisionEndpoint.cs`
- Modify: `tests/smash-dates.IntegrationTests/Endpoints/CreateDivisionEndpointTests.cs`

- [ ] **Step 1: Update the endpoint authorisation**

Open `Endpoints/Divisions/CreateDivisionEndpoint.cs`. The current `MapCreateDivisionEndpoint` line is:

```csharp
app.MapPost("", Handle)
    .RequireAuthorization(AuthorizationPolicies.SystemAdmin);
```

Change to drop the policy attribute (the group's `.RequireAuthorization()` already requires sign-in):

```csharp
app.MapPost("/", Handle);
```

Then inside `Handle`, after loading the league but before validating the body, add the inline check:

```csharp
var league = await leagues.GetByIdAsync(leagueId, ct);
if (league is null) return Results.NotFound();

var authz = await LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, leagueAdmins, ct);
if (authz is not null) return authz;
```

The handler signature must now accept `ClaimsPrincipal principal` and `ILeagueAdminRepository leagueAdmins`. Update the signature accordingly and add the necessary `using` for `smash_dates.Services.Auth` and `System.Security.Claims`.

(Also flip `MapPost("")` to `MapPost("/")` while you're here for consistency with the rest of the codebase.)

- [ ] **Step 2: Update tests**

`CreateDivisionEndpointTests.cs` currently uses `LoginAsSystemAdminAsync` for the happy-path test. Add a new test confirming a non-system LeagueAdmin can also create divisions:

```csharp
[Fact]
public async Task Post_AsLeagueAdmin_CreatesDivision_Returns201()
{
    var sys = await Seeder.CreateSystemAdminUserAsync("sys@example.com", "correct-horse-battery");
    var leagueAdmin = await Seeder.CreateUserAsync("la@example.com", "correct-horse-battery");
    var leagueId = await Seeder.CreateLeagueAsync("NL", sys.Id);
    await Seeder.GrantLeagueAdminAsync(leagueId, leagueAdmin.Id, sys.Id);
    await Client.PostAsJsonAsync("/api/auth/login", new { email = "la@example.com", password = "correct-horse-battery" });

    var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/divisions", new
    {
        name = "Mens 1",
        gender = "Mens",
        rank = 1,
        rubbersPerMatch = 9,
        winPoints = 2,
        drawPoints = 1,
        lossPoints = 0,
    });

    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

`Post_AsNonAdmin_Returns403` keeps its meaning — a plain user (no LeagueAdmin grant) gets 403.

- [ ] **Step 3: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj
git add Endpoints/Divisions/CreateDivisionEndpoint.cs tests/smash-dates.IntegrationTests/Endpoints/CreateDivisionEndpointTests.cs
git commit -m "feat(divisions): allow LeagueAdmin to create divisions in their league"
```

---

### Task 9: `GET /api/users/lookup?email=...` endpoint

**Files:**
- Create: `Endpoints/Users/UserEndpoints.cs`
- Create: `Endpoints/Users/LookupUserEndpoint.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/LookupUserEndpointTests.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Write the group**

`Endpoints/Users/UserEndpoints.cs`:

```csharp
namespace smash_dates.Endpoints.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .RequireAuthorization();

        group.MapLookupUserEndpoint();
        return app;
    }
}
```

- [ ] **Step 2: Write the lookup endpoint**

`Endpoints/Users/LookupUserEndpoint.cs`:

```csharp
using System.Net.Mail;
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Users;

public static class LookupUserEndpoint
{
    private const int MaxEmailLength = 254;

    public sealed record UserLookupResponse(Guid Id, string Email, string? DisplayName);

    public static IEndpointRouteBuilder MapLookupUserEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/lookup", Handle);
        return app;
    }

    private static async Task<IResult> Handle(string? email, IUserRepository users, CancellationToken ct)
    {
        var trimmed = (email ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxEmailLength || !MailAddress.TryCreate(trimmed, out _))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid email");
        }

        var user = await users.GetByEmailAsync(trimmed, ct);
        return user is null
            ? Results.NotFound()
            : Results.Ok(new UserLookupResponse(user.Id, user.Email, user.DisplayName));
    }
}
```

- [ ] **Step 3: Register in Program.cs**

Add `using smash_dates.Endpoints.Users;` and `app.MapUserEndpoints();` after `app.MapLeagueAdminEndpoints();`.

- [ ] **Step 4: Write tests**

`tests/smash-dates.IntegrationTests/Endpoints/LookupUserEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using smash_dates.Endpoints.Users;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class LookupUserEndpointTests : IntegrationTestBase
{
    public LookupUserEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/users/lookup?email=foo@bar.com");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_KnownEmail_ReturnsUser()
    {
        await Seeder.CreateUserAsync("target@example.com", "correct-horse-battery", displayName: "Target");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync("/api/users/lookup?email=target@example.com");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LookupUserEndpoint.UserLookupResponse>();
        body!.Email.Should().Be("target@example.com");
        body.DisplayName.Should().Be("Target");
    }

    [Fact]
    public async Task Get_UnknownEmail_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync("/api/users/lookup?email=nobody@example.com");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_InvalidEmail_Returns400()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync("/api/users/lookup?email=not-an-email");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 5: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj
git add Endpoints/Users Program.cs tests/smash-dates.IntegrationTests/Endpoints/LookupUserEndpointTests.cs
git commit -m "feat(users): add /api/users/lookup endpoint"
```

---

### Task 10: Drop `CreatedBy` from `GET /api/leagues/{id}` response (PR follow-up #3)

**Files:**
- Modify: `Endpoints/Leagues/GetLeagueEndpoint.cs`
- Modify: `tests/smash-dates.IntegrationTests/Endpoints/GetLeagueEndpointTests.cs`
- Modify: `ClientApp/src/app/features/admin/leagues.api.ts`

- [ ] **Step 1: Update the response record**

In `Endpoints/Leagues/GetLeagueEndpoint.cs`, change:

```csharp
public sealed record LeagueDetail(Guid Id, string Name, string? Description, Guid CreatedBy);
```

to:

```csharp
public sealed record LeagueDetail(Guid Id, string Name, string? Description);
```

And the handler return:

```csharp
return Results.Ok(new LeagueDetail(league.Id, league.Name, league.Description));
```

- [ ] **Step 2: Update the existing test**

In `GetLeagueEndpointTests.cs`, the `Get_ExistingLeague_Returns200` test references `body!.Name` and `body.Description` only — no change needed beyond confirming `CreatedBy` is no longer accessed.

- [ ] **Step 3: Update the Angular API client**

In `ClientApp/src/app/features/admin/leagues.api.ts`, change:

```typescript
export interface LeagueDetail extends LeagueSummary {
  createdBy: string;
}
```

to:

```typescript
export type LeagueDetail = LeagueSummary;
```

- [ ] **Step 4: Run + commit**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj
cd ClientApp && npm run build && cd ..
git add Endpoints/Leagues/GetLeagueEndpoint.cs ClientApp/src/app/features/admin/leagues.api.ts
git commit -m "fix(leagues): drop CreatedBy from GET /api/leagues/{id} response"
```

---

### Task 11: Convert route templates to `/` for consistency (PR follow-up #2)

**Files:**
- Modify: `Endpoints/Leagues/CreateLeagueEndpoint.cs`, `ListLeaguesEndpoint.cs`
- Modify: `Endpoints/Divisions/CreateDivisionEndpoint.cs`, `ListDivisionsEndpoint.cs`
- Modify: `Endpoints/LeagueAdmins/ListLeagueAdminsEndpoint.cs`, `GrantLeagueAdminEndpoint.cs` (already `/` from Tasks 5/6)
- Modify: any test that asserts a `Location` header path

- [ ] **Step 1: Replace `MapPost("", ...)` and `MapGet("", ...)` with `/`**

Open each listed file and change `MapPost("", Handle)` → `MapPost("/", Handle)` and `MapGet("", Handle)` → `MapGet("/", Handle)`. The empty-string trick was a workaround that's no longer needed; the trailing-slash 404 doesn't occur in current ASP.NET Core when the group prefix doesn't end with a slash.

- [ ] **Step 2: Run all integration tests**

```
dotnet test --project tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj
```

If any test fails with 404 on a route that previously worked, revert to `""` for that specific endpoint and add a comment explaining why. (We don't expect this — the convention should be uniform.)

- [ ] **Step 3: Commit**

```
git add Endpoints
git commit -m "refactor: normalise endpoint route templates to /"
```

---

### Task 12: Angular admin-management UI

**Files:**
- Modify: `ClientApp/src/app/features/admin/leagues.api.ts` — add admin + lookup methods.
- Modify: `ClientApp/src/app/features/admin/admin.routes.ts` — register `/admin/leagues/:id/admins`.
- Create: `ClientApp/src/app/features/admin/league-admins.page.ts`.
- Modify: `ClientApp/src/app/features/admin/league-detail.page.ts` — add a link to the admins page.

- [ ] **Step 1: Extend the API client**

In `ClientApp/src/app/features/admin/leagues.api.ts`, append:

```typescript
export interface LeagueAdminSummary {
  userId: string;
  email: string;
  displayName: string | null;
  grantedAt: string;
}

export interface UserLookup {
  id: string;
  email: string;
  displayName: string | null;
}
```

Add methods to the `LeaguesApi` class:

```typescript
listAdmins(leagueId: string): Observable<LeagueAdminSummary[]> {
  return this.http.get<LeagueAdminSummary[]>(`/api/leagues/${leagueId}/admins`);
}

grantAdmin(leagueId: string, userId: string): Observable<void> {
  return this.http.post<void>(`/api/leagues/${leagueId}/admins`, { userId });
}

revokeAdmin(leagueId: string, userId: string): Observable<void> {
  return this.http.delete<void>(`/api/leagues/${leagueId}/admins/${userId}`);
}

lookupUser(email: string): Observable<UserLookup> {
  const params = new HttpParams().set('email', email);
  return this.http.get<UserLookup>('/api/users/lookup', { params });
}
```

Add `HttpParams` to the `@angular/common/http` import.

Also update `CreateLeagueRequest` to include the first-admin field:

```typescript
export interface CreateLeagueRequest {
  name: string;
  description: string | null;
  firstLeagueAdminUserId: string;
}
```

- [ ] **Step 2: Update the leagues-list create-league form**

In `ClientApp/src/app/features/admin/leagues-list.page.ts`:

- Add a `firstAdminEmail: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] })` field to the form.
- In `onCreate()`, lookup the user first, surface 404 as "no user with that email", then call `create` with the resolved id.

Updated `onCreate`:

```typescript
protected onCreate(): void {
  const { name, description, firstAdminEmail } = this.form.getRawValue();
  const trimmedName = name.trim();
  if (!trimmedName) return;

  this.submitting.set(true);
  this.error.set(null);
  const trimmedDescription = description.trim();

  this.api.lookupUser(firstAdminEmail.trim()).subscribe({
    next: (user) => {
      this.api
        .create({
          name: trimmedName,
          description: trimmedDescription ? trimmedDescription : null,
          firstLeagueAdminUserId: user.id,
        })
        .subscribe({
          next: () => {
            this.submitting.set(false);
            this.form.reset({ name: '', description: '', firstAdminEmail: '' });
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
      this.error.set('No registered user with that email — they must register first.');
    },
  });
}
```

Add a corresponding `<label>` block in the template, with the same brutalist styling as the existing inputs.

- [ ] **Step 3: Write the admins page**

`ClientApp/src/app/features/admin/league-admins.page.ts`:

```typescript
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import { LeagueAdminSummary, LeaguesApi } from './leagues.api';

@Component({
  selector: 'app-league-admins-page',
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
          [routerLink]="['/admin/leagues', leagueId()]"
          class="font-mono text-xs uppercase tracking-wider text-slate-500 hover:underline"
          >← back to league</a
        >
        <h1 class="mt-2 font-mono text-2xl font-semibold text-slate-900">League admins</h1>

        <ul class="mt-6 divide-y divide-slate-200 rounded-md border border-slate-200 bg-white">
          @for (admin of admins(); track admin.userId) {
            <li class="flex items-center justify-between px-4 py-3 font-mono text-sm">
              <span>
                {{ admin.displayName ?? admin.email }}
                <span class="ml-2 text-slate-500">{{ admin.email }}</span>
              </span>
              <button
                type="button"
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
          [formGroup]="form"
          (ngSubmit)="onGrant()"
          class="mt-6 grid gap-3 rounded-md border border-slate-200 bg-white p-4 shadow-sm"
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
            [disabled]="submitting() || form.invalid"
            class="justify-self-start rounded-md bg-slate-900 px-4 py-2 font-mono text-sm font-medium text-amber-300 disabled:opacity-50"
          >
            {{ submitting() ? 'Granting…' : 'Grant admin' }}
          </button>
          @if (error()) {
            <p class="font-mono text-sm text-red-600" role="alert">{{ error() }}</p>
          }
        </form>
      </main>
    </div>
  `,
})
export default class LeagueAdminsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(LeaguesApi);

  protected readonly leagueId = signal('');
  protected readonly admins = signal<LeagueAdminSummary[]>([]);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  constructor() {
    this.route.paramMap
      .pipe(
        tap((p) => this.leagueId.set(p.get('id') ?? '')),
        switchMap((p) => this.api.listAdmins(p.get('id') ?? '')),
      )
      .subscribe({
        next: (rows) => this.admins.set(rows),
        error: () => this.error.set('Failed to load admins.'),
      });
  }

  protected onGrant(): void {
    const email = this.form.getRawValue().email.trim();
    if (!email) return;
    this.submitting.set(true);
    this.error.set(null);
    this.api.lookupUser(email).subscribe({
      next: (user) => {
        this.api.grantAdmin(this.leagueId(), user.id).subscribe({
          next: () => {
            this.submitting.set(false);
            this.form.reset({ email: '' });
            this.refresh();
          },
          error: (err: { error?: { title?: string } }) => {
            this.submitting.set(false);
            this.error.set(err?.error?.title ?? 'Grant failed.');
          },
        });
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('No registered user with that email.');
      },
    });
  }

  protected onRevoke(userId: string): void {
    this.error.set(null);
    this.api.revokeAdmin(this.leagueId(), userId).subscribe({
      next: () => this.refresh(),
      error: (err: { error?: { title?: string } }) =>
        this.error.set(err?.error?.title ?? 'Revoke failed.'),
    });
  }

  private refresh(): void {
    this.api.listAdmins(this.leagueId()).subscribe({
      next: (rows) => this.admins.set(rows),
    });
  }
}
```

- [ ] **Step 4: Register the route**

In `ClientApp/src/app/features/admin/admin.routes.ts`, add:

```typescript
{
  path: 'leagues/:id/admins',
  title: 'League admins · smash-dates',
  loadComponent: () => import('./league-admins.page'),
},
```

- [ ] **Step 5: Link from league detail**

In `ClientApp/src/app/features/admin/league-detail.page.ts`, after the league header `<p>` block, add:

```html
<a
  [routerLink]="['/admin/leagues', leagueId, 'admins']"
  class="mt-2 inline-block font-mono text-xs uppercase tracking-wider text-slate-500 hover:underline"
  >manage admins →</a
>
```

Add `RouterLink` to the `imports` array of the component. Expose `leagueId` to the template by changing the existing `private leagueId = '';` to `protected leagueId = '';`.

- [ ] **Step 6: Build + test**

```
cd ClientApp
npm test
npm run build
cd ..
```

Expect all green and a clean build.

- [ ] **Step 7: Commit**

```
git add ClientApp/src/app/features/admin
git commit -m "feat(client): manage LeagueAdmins UI"
```

---

### Task 13: README update + final sweep

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Append to README**

Under the "Foundation slice" section, add:

```markdown
## Slice 2a — League admin grants

- `POST /api/leagues` *(SystemAdmin)* — body now requires `firstLeagueAdminUserId`. League + first admin grant are created atomically.
- `GET  /api/leagues/{id}/admins` *(authenticated)*
- `POST /api/leagues/{id}/admins` *(LeagueAdmin@thisLeague | SystemAdmin)* — body `{ userId }`.
- `DELETE /api/leagues/{id}/admins/{userId}` *(LeagueAdmin@thisLeague | SystemAdmin)* — last-admin removal returns 409 unless caller is SystemAdmin.
- `POST /api/leagues/{leagueId}/divisions` *(LeagueAdmin@thisLeague | SystemAdmin)* — previously SystemAdmin-only.
- `GET  /api/users/lookup?email=...` *(authenticated)* — resolves email → userId for granter UIs.

Frontend route added: `/admin/leagues/:id/admins`.
```

- [ ] **Step 2: Full test sweep**

```
dotnet test
cd ClientApp && npm test && npm run build && cd ..
```

All green.

- [ ] **Step 3: Commit**

```
git add README.md
git commit -m "docs(readme): document slice 2a endpoints"
```

---

## Self-Review

**Spec coverage:**
- LeagueAdmin model + multi-admin support ✓ (Tasks 1, 2)
- Atomic League creation with first admin ✓ (Task 4)
- `LeagueAdmin@League | SystemAdmin` policy via inline helper ✓ (Task 3)
- Grant + revoke + list endpoints with last-admin invariant ✓ (Tasks 5–7)
- Division endpoint authz migration ✓ (Task 8)
- User lookup ✓ (Task 9)
- PR follow-ups #2 (route consistency) and #3 (CreatedBy leak) ✓ (Tasks 10, 11)
- Angular admin-management UI ✓ (Task 12)
- README ✓ (Task 13)

**Deferred to slice 2b:** Clubs, ClubAdmin grants, Memberships, related Angular UI.

**Placeholder scan:** none.

**Type consistency:** `ILeagueAdminRepository.IsAdminAsync(leagueId, userId, ct)` — same arg order everywhere it's called (Tasks 3, 6, 7, 8). `LeagueAuthorizer.RequireLeagueAdminAsync(principal, leagueId, admins, ct)` — same call signature in Tasks 6, 7, 8. The endpoint route templates use `/{leagueId:guid}` consistently.
