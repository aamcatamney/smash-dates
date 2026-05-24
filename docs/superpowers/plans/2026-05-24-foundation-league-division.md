# Foundation: League + Division Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the foundation tracer-bullet slice for Smash Dates: a SystemAdmin can create Leagues, and a SystemAdmin can create Divisions inside a League. Exercises full stack (migration → Dapper repo → minimal-API endpoint → Angular admin page) so subsequent slices (Clubs, Teams, Venues, Seasons, Scheduler) have a working pattern to follow.

**Architecture:**
- Backend follows the established Auth pattern: one endpoint per file under `Endpoints/`, repository pattern with Dapper + Npgsql, models in `Models/`, repositories in `Repositories/`, migrations as numbered SQL files in `Migrations/Scripts/`.
- Authorisation: a `SystemAdmin` policy is added; the first user to register through `/api/auth/register` is automatically promoted to SystemAdmin via a transactional check inside `UserRepository.CreateAsync`.
- Frontend: a new `admin` feature module is added under `ClientApp/src/app/features/admin/` with two routes — `/admin/leagues` (list + create) and `/admin/leagues/:id` (detail + divisions). Reachable only by authenticated users who are SystemAdmin (gate enforced server-side; the menu link is hidden by querying `/api/auth/me` for the flag).

**Tech Stack:** .NET 10 minimal API · Dapper · Npgsql · PostgreSQL · DbUp (migrations) · Angular 18 · xUnit · FluentAssertions · Testcontainers (via existing `PostgresFixture`).

**Out of scope for this slice:** Clubs, Teams, Venues, Seasons, Weeks, Blocked Dates, Club–League membership, ClubAdmin / LeagueAdmin role grants, the scheduler engine, match results, frontend brutalist visual polish (a functional shell only — the bold visual treatment lands in a later slice).

---

## File Structure

**Created:**
- `Migrations/Scripts/0003_system_admin_flag.sql` — add `is_system_admin` to `users`.
- `Migrations/Scripts/0004_create_leagues.sql` — `leagues` table.
- `Migrations/Scripts/0005_create_divisions.sql` — `divisions` table with FK to `leagues`.
- `Models/League.cs` — POCO matching `leagues` row.
- `Models/Division.cs` — POCO matching `divisions` row.
- `Models/DivisionGender.cs` — enum `Mens` / `Ladies` / `Mixed`.
- `Repositories/ILeagueRepository.cs` / `LeagueRepository.cs` — CRUD over `leagues`.
- `Repositories/IDivisionRepository.cs` / `DivisionRepository.cs` — CRUD over `divisions`.
- `Endpoints/Leagues/LeagueEndpoints.cs` — group registration `/api/leagues`.
- `Endpoints/Leagues/CreateLeagueEndpoint.cs` — `POST /api/leagues`.
- `Endpoints/Leagues/ListLeaguesEndpoint.cs` — `GET /api/leagues`.
- `Endpoints/Leagues/GetLeagueEndpoint.cs` — `GET /api/leagues/{id}`.
- `Endpoints/Divisions/DivisionEndpoints.cs` — group registration `/api/leagues/{leagueId}/divisions`.
- `Endpoints/Divisions/CreateDivisionEndpoint.cs` — `POST /api/leagues/{leagueId}/divisions`.
- `Endpoints/Divisions/ListDivisionsEndpoint.cs` — `GET /api/leagues/{leagueId}/divisions`.
- `Services/Auth/AuthorizationPolicies.cs` — defines `SystemAdmin` policy name + claim type constant.
- `ClientApp/src/app/features/admin/admin.routes.ts` — Angular feature routes.
- `ClientApp/src/app/features/admin/leagues-list.page.ts` — list + create form.
- `ClientApp/src/app/features/admin/league-detail.page.ts` — detail + divisions list/create.
- `ClientApp/src/app/features/admin/leagues.api.ts` — typed HTTP client.
- `tests/smash-dates.IntegrationTests/Endpoints/CreateLeagueEndpointTests.cs`
- `tests/smash-dates.IntegrationTests/Endpoints/ListLeaguesEndpointTests.cs`
- `tests/smash-dates.IntegrationTests/Endpoints/GetLeagueEndpointTests.cs`
- `tests/smash-dates.IntegrationTests/Endpoints/CreateDivisionEndpointTests.cs`
- `tests/smash-dates.IntegrationTests/Endpoints/ListDivisionsEndpointTests.cs`
- `tests/smash-dates.IntegrationTests/Repositories/LeagueRepositoryTests.cs`
- `tests/smash-dates.IntegrationTests/Repositories/DivisionRepositoryTests.cs`

**Modified:**
- `Models/User.cs` — add `bool IsSystemAdmin` property.
- `Repositories/UserRepository.cs` — `CreateAsync` becomes transactional, promotes first user to SystemAdmin; `SelectColumns` includes new column.
- `Endpoints/Auth/RegisterEndpoint.cs` — read `IsSystemAdmin` after create, add as claim on the principal.
- `Endpoints/Auth/LoginEndpoint.cs` — add `IsSystemAdmin` claim from `User` to the principal.
- `Endpoints/Auth/MeEndpoint.cs` — return `IsSystemAdmin` so the frontend can show/hide the admin menu.
- `Program.cs` — register `ILeagueRepository`, `IDivisionRepository`, the `SystemAdmin` authorization policy, and `app.MapLeagueEndpoints()` / `app.MapDivisionEndpoints()`.
- `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs` — add `CreateLeagueAsync`, `CreateDivisionAsync`, and `CreateSystemAdminUserAsync` helpers; existing `CreateUserAsync` continues to make non-admin users.
- `ClientApp/src/app/core/auth/user.model.ts` — add `isSystemAdmin: boolean`.
- `ClientApp/src/app/core/auth/auth.store.ts` — surface `isSystemAdmin`.
- `ClientApp/src/app/app.routes.ts` — add lazy `admin` route guarded by a new `systemAdminGuard`.
- `ClientApp/src/app/core/auth/auth.guard.ts` (or sibling new file) — add `systemAdminGuard`.
- `README.md` — document the foundation slice and how to bootstrap a SystemAdmin.

---

### Task 1: Add `is_system_admin` column on `users`

**Files:**
- Create: `Migrations/Scripts/0003_system_admin_flag.sql`

- [ ] **Step 1: Write the migration**

Create `Migrations/Scripts/0003_system_admin_flag.sql`:

```sql
ALTER TABLE users
    ADD COLUMN is_system_admin boolean NOT NULL DEFAULT false;

CREATE UNIQUE INDEX ux_users_single_system_admin_bootstrap
    ON users ((1))
    WHERE is_system_admin = true AND email = 'bootstrap-placeholder@invalid';
-- Placeholder partial index keeps migration idempotent; the bootstrap logic
-- in UserRepository enforces "first registered user becomes admin".
```

Note: the partial index above is an inert no-op (no row will ever match `email = 'bootstrap-placeholder@invalid'`). It exists so the migration's intent ("only one initial bootstrap path") is documented in schema. The actual rule lives in `UserRepository.CreateAsync` (Task 4).

- [ ] **Step 2: Mark the SQL file as embedded**

The project already uses `WithScriptsEmbeddedInAssembly` (see `Migrations/DbMigrator.cs:13`). Verify `smash-dates.csproj` has the `<EmbeddedResource Include="Migrations\Scripts\**\*.sql" />` glob — if it does not, add it.

- [ ] **Step 3: Update the migrator test fixture asserts the new script applies**

Open `tests/smash-dates.IntegrationTests/Migrations/DbMigratorTests.cs` and run the existing tests:

```bash
dotnet test tests/smash-dates.IntegrationTests/smash-dates.IntegrationTests.csproj --filter FullyQualifiedName~DbMigratorTests
```

Expected: PASS. (The existing test confirms all scripts apply cleanly; no edit required if it scans all scripts.)

- [ ] **Step 4: Commit**

```bash
git add Migrations/Scripts/0003_system_admin_flag.sql smash-dates.csproj
git commit -m "feat(db): add is_system_admin column on users"
```

---

### Task 2: Add `IsSystemAdmin` to the `User` model

**Files:**
- Modify: `Models/User.cs`

- [ ] **Step 1: Write a failing integration test for `UserRepository.GetByIdAsync`**

Modify `tests/smash-dates.IntegrationTests/Repositories/UserRepositoryTests.cs` — add:

```csharp
[Fact]
public async Task GetByIdAsync_NewUser_HasIsSystemAdminFalse()
{
    var user = await Seeder.CreateUserAsync("plain@example.com", "correct-horse-battery");
    var repo = new UserRepository(Fixture.ConnectionFactory);

    var loaded = await repo.GetByIdAsync(user.Id);

    loaded.Should().NotBeNull();
    loaded!.IsSystemAdmin.Should().BeFalse();
}
```

- [ ] **Step 2: Run the test to see it fail**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~UserRepositoryTests.GetByIdAsync_NewUser_HasIsSystemAdminFalse
```

Expected: compilation failure — `User` has no `IsSystemAdmin`.

- [ ] **Step 3: Add the property**

Edit `Models/User.cs` to:

```csharp
namespace smash_dates.Models;

public sealed class User
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public bool IsActive { get; init; }
    public bool IsSystemAdmin { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
```

- [ ] **Step 4: Update `SelectColumns` in `UserRepository`**

Edit `Repositories/UserRepository.cs:9-10`:

```csharp
private const string SelectColumns =
    "id, email, password_hash, display_name, is_active, is_system_admin, created_at, updated_at";
```

- [ ] **Step 5: Run the test, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~UserRepositoryTests.GetByIdAsync_NewUser_HasIsSystemAdminFalse
```

Expected: PASS. Dapper's `MatchNamesWithUnderscores = true` (`Data/DapperConfiguration.cs:11`) maps `is_system_admin` → `IsSystemAdmin` automatically.

- [ ] **Step 6: Commit**

```bash
git add Models/User.cs Repositories/UserRepository.cs tests/smash-dates.IntegrationTests/Repositories/UserRepositoryTests.cs
git commit -m "feat(users): expose is_system_admin on User model"
```

---

### Task 3: Promote first registered user to SystemAdmin

**Files:**
- Modify: `Repositories/UserRepository.cs`
- Modify: `tests/smash-dates.IntegrationTests/Repositories/UserRepositoryTests.cs`

- [ ] **Step 1: Write failing test for first-user promotion**

Add to `UserRepositoryTests`:

```csharp
[Fact]
public async Task CreateAsync_WhenNoUsersExist_PromotesNewUserToSystemAdmin()
{
    var repo = new UserRepository(Fixture.ConnectionFactory);

    var id = await repo.CreateAsync("first@example.com", "hash", null);
    var loaded = await repo.GetByIdAsync(id);

    loaded!.IsSystemAdmin.Should().BeTrue();
}

[Fact]
public async Task CreateAsync_WhenUsersExist_DoesNotPromote()
{
    await Seeder.CreateUserAsync("first@example.com", "correct-horse-battery");
    var repo = new UserRepository(Fixture.ConnectionFactory);

    var secondId = await repo.CreateAsync("second@example.com", "hash", null);
    var loaded = await repo.GetByIdAsync(secondId);

    loaded!.IsSystemAdmin.Should().BeFalse();
}
```

- [ ] **Step 2: Run, expect FAIL**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~UserRepositoryTests.CreateAsync_When
```

Expected: both tests fail (`IsSystemAdmin` is false in both cases — the existing `CreateAsync` never sets it).

- [ ] **Step 3: Make `CreateAsync` transactional with first-user check**

Edit `Repositories/UserRepository.cs:39-49`:

```csharp
public async Task<Guid> CreateAsync(string email, string passwordHash, string? displayName, CancellationToken ct = default)
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

    var hasAnyUser = await conn.ExecuteScalarAsync<bool>(
        new CommandDefinition(
            "SELECT EXISTS(SELECT 1 FROM users)",
            transaction: tx,
            cancellationToken: ct));

    var id = await conn.ExecuteScalarAsync<Guid>(
        new CommandDefinition(
            @"INSERT INTO users (email, password_hash, display_name, is_system_admin)
              VALUES (lower(@email), @passwordHash, @displayName, @isSystemAdmin)
              RETURNING id",
            new { email, passwordHash, displayName, isSystemAdmin = !hasAnyUser },
            transaction: tx,
            cancellationToken: ct));

    tx.Commit();
    return id;
}
```

- [ ] **Step 4: Run, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~UserRepositoryTests.CreateAsync_When
```

Expected: both PASS.

- [ ] **Step 5: Commit**

```bash
git add Repositories/UserRepository.cs tests/smash-dates.IntegrationTests/Repositories/UserRepositoryTests.cs
git commit -m "feat(auth): promote first registered user to SystemAdmin"
```

---

### Task 4: Add `SystemAdmin` claim + authorization policy

**Files:**
- Create: `Services/Auth/AuthorizationPolicies.cs`
- Modify: `Endpoints/Auth/RegisterEndpoint.cs`
- Modify: `Endpoints/Auth/LoginEndpoint.cs`
- Modify: `Endpoints/Auth/MeEndpoint.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Write the policies file**

Create `Services/Auth/AuthorizationPolicies.cs`:

```csharp
namespace smash_dates.Services.Auth;

public static class AuthorizationPolicies
{
    public const string SystemAdmin = "SystemAdmin";
    public const string SystemAdminClaim = "smash:system_admin";
}
```

- [ ] **Step 2: Register the policy in Program.cs**

Replace line `builder.Services.AddAuthorization();` in `Program.cs:93` with:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.SystemAdmin, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(AuthorizationPolicies.SystemAdminClaim, "true"));
});
```

Add `using smash_dates.Services.Auth;` if not already present.

- [ ] **Step 3: Issue the claim in `RegisterEndpoint`**

In `Endpoints/Auth/RegisterEndpoint.cs`, after `users.CreateAsync`, fetch the created user to learn its `IsSystemAdmin`, then add a claim. Replace lines 58–69:

```csharp
var id = await users.CreateAsync(email, hash, displayName, ct);
var created = await users.GetByIdAsync(id, ct)
    ?? throw new InvalidOperationException("User vanished immediately after creation.");

var claims = new List<Claim>
{
    new(ClaimTypes.NameIdentifier, id.ToString()),
    new(ClaimTypes.Email, email.ToLowerInvariant()),
};
if (created.IsSystemAdmin)
{
    claims.Add(new Claim(AuthorizationPolicies.SystemAdminClaim, "true"));
}

var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

await http.SignInAsync(
    CookieAuthenticationDefaults.AuthenticationScheme,
    new ClaimsPrincipal(identity),
    new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14) });
```

Add `using smash_dates.Services.Auth;` at the top.

- [ ] **Step 4: Do the same in `LoginEndpoint`**

Open `Endpoints/Auth/LoginEndpoint.cs`. Locate where it builds the `ClaimsIdentity` after a successful password verification. Add the conditional `SystemAdminClaim` claim mirroring Task 4 Step 3. (Inspect the existing file first; the precise lines depend on its current shape.)

- [ ] **Step 5: Return the flag from `MeEndpoint`**

Open `Endpoints/Auth/MeEndpoint.cs`. The endpoint currently shapes a response from `ClaimsPrincipal`. Add `IsSystemAdmin` (bool) to the response record, populated from `User.FindFirstValue(AuthorizationPolicies.SystemAdminClaim) == "true"`.

- [ ] **Step 6: Update the `LoginEndpoint.UserResponse` record**

This record is reused by `RegisterEndpoint` and `MeEndpoint`. Add `bool IsSystemAdmin` to it. Adjust all three endpoints to pass the value when constructing.

- [ ] **Step 7: Update the auth integration tests to assert the flag**

In `tests/smash-dates.IntegrationTests/Endpoints/RegisterEndpointTests.cs`:

```csharp
[Fact]
public async Task Register_FirstUser_IsSystemAdmin()
{
    var response = await Client.PostAsJsonAsync("/api/auth/register", new
    {
        email = "first@example.com",
        password = "correct-horse-battery",
    });

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<LoginEndpoint.UserResponse>();
    body!.IsSystemAdmin.Should().BeTrue();
}

[Fact]
public async Task Register_SecondUser_IsNotSystemAdmin()
{
    await Seeder.CreateUserAsync("first@example.com", "correct-horse-battery");

    var response = await Client.PostAsJsonAsync("/api/auth/register", new
    {
        email = "second@example.com",
        password = "correct-horse-battery",
    });

    var body = await response.Content.ReadFromJsonAsync<LoginEndpoint.UserResponse>();
    body!.IsSystemAdmin.Should().BeFalse();
}
```

- [ ] **Step 8: Run all auth tests, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~Endpoints
```

Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add Services/Auth/AuthorizationPolicies.cs Endpoints/Auth Program.cs tests/smash-dates.IntegrationTests/Endpoints/RegisterEndpointTests.cs
git commit -m "feat(auth): issue SystemAdmin claim and policy"
```

---

### Task 5: Create `leagues` table migration

**Files:**
- Create: `Migrations/Scripts/0004_create_leagues.sql`

- [ ] **Step 1: Write the migration**

Create `Migrations/Scripts/0004_create_leagues.sql`:

```sql
CREATE TABLE leagues (
    id          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name        text NOT NULL,
    description text NULL,
    created_by  uuid NOT NULL REFERENCES users(id),
    created_at  timestamptz NOT NULL DEFAULT now(),
    updated_at  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_leagues_name_lower ON leagues (lower(name));
```

- [ ] **Step 2: Run migrator integration test, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~DbMigratorTests
```

- [ ] **Step 3: Commit**

```bash
git add Migrations/Scripts/0004_create_leagues.sql
git commit -m "feat(db): add leagues table"
```

---

### Task 6: Add `League` model + repository

**Files:**
- Create: `Models/League.cs`
- Create: `Repositories/ILeagueRepository.cs`
- Create: `Repositories/LeagueRepository.cs`
- Create: `tests/smash-dates.IntegrationTests/Repositories/LeagueRepositoryTests.cs`
- Modify: `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`

- [ ] **Step 1: Write the model**

`Models/League.cs`:

```csharp
namespace smash_dates.Models;

public sealed class League
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid CreatedBy { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
```

- [ ] **Step 2: Write the failing repository tests**

`tests/smash-dates.IntegrationTests/Repositories/LeagueRepositoryTests.cs`:

```csharp
using FluentAssertions;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

public sealed class LeagueRepositoryTests : IntegrationTestBase
{
    public LeagueRepositoryTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateAsync_PersistsLeague()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var repo = new LeagueRepository(Fixture.ConnectionFactory);

        var id = await repo.CreateAsync("North London", "Top division of NL", admin.Id);

        var loaded = await repo.GetByIdAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("North London");
        loaded.Description.Should().Be("Top division of NL");
        loaded.CreatedBy.Should().Be(admin.Id);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllLeagues_OrderedByName()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var repo = new LeagueRepository(Fixture.ConnectionFactory);
        await repo.CreateAsync("Beta", null, admin.Id);
        await repo.CreateAsync("Alpha", null, admin.Id);

        var results = await repo.ListAsync();

        results.Select(r => r.Name).Should().ContainInOrder("Alpha", "Beta");
    }

    [Fact]
    public async Task CreateAsync_DuplicateNameCaseInsensitive_Throws()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var repo = new LeagueRepository(Fixture.ConnectionFactory);
        await repo.CreateAsync("North London", null, admin.Id);

        Func<Task> act = () => repo.CreateAsync("north london", null, admin.Id);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
```

- [ ] **Step 3: Run, expect FAIL (no types)**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~LeagueRepositoryTests
```

- [ ] **Step 4: Write the interface**

`Repositories/ILeagueRepository.cs`:

```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

public interface ILeagueRepository
{
    Task<League?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<League>> ListAsync(CancellationToken ct = default);
    Task<Guid> CreateAsync(string name, string? description, Guid createdBy, CancellationToken ct = default);
}
```

- [ ] **Step 5: Write the repository**

`Repositories/LeagueRepository.cs`:

```csharp
using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class LeagueRepository : ILeagueRepository
{
    private const string SelectColumns =
        "id, name, description, created_by, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public LeagueRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<League?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<League>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM leagues WHERE id = @id",
                new { id },
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<League>> ListAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<League>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM leagues ORDER BY name",
                cancellationToken: ct));
        return rows.AsList();
    }

    public async Task<Guid> CreateAsync(string name, string? description, Guid createdBy, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO leagues (name, description, created_by)
                  VALUES (@name, @description, @createdBy)
                  RETURNING id",
                new { name, description, createdBy },
                cancellationToken: ct));
    }
}
```

- [ ] **Step 6: Run repository tests, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~LeagueRepositoryTests
```

- [ ] **Step 7: Add a seeder helper**

Add to `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`:

```csharp
public async Task<Guid> CreateLeagueAsync(string name, Guid createdBy, string? description = null)
{
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    return await conn.ExecuteScalarAsync<Guid>(
        @"INSERT INTO leagues (name, description, created_by)
          VALUES (@name, @description, @createdBy)
          RETURNING id",
        new { name, description, createdBy });
}
```

- [ ] **Step 8: Commit**

```bash
git add Models/League.cs Repositories/ILeagueRepository.cs Repositories/LeagueRepository.cs tests/smash-dates.IntegrationTests/Repositories/LeagueRepositoryTests.cs tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs
git commit -m "feat(leagues): add League model and Dapper repository"
```

---

### Task 7: League endpoints — create / list / get

**Files:**
- Create: `Endpoints/Leagues/LeagueEndpoints.cs`
- Create: `Endpoints/Leagues/CreateLeagueEndpoint.cs`
- Create: `Endpoints/Leagues/ListLeaguesEndpoint.cs`
- Create: `Endpoints/Leagues/GetLeagueEndpoint.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/CreateLeagueEndpointTests.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/ListLeaguesEndpointTests.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/GetLeagueEndpointTests.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Write failing `CreateLeague` integration test**

`tests/smash-dates.IntegrationTests/Endpoints/CreateLeagueEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateLeagueEndpointTests : IntegrationTestBase
{
    public CreateLeagueEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsSystemAdmin_CreatesLeague_Returns201()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new
        {
            name = "North London",
            description = "Top division",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.ToString().Should().StartWith("/api/leagues/");
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "X" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "X" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_DuplicateName_Returns409()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);
        await Client.PostAsJsonAsync("/api/leagues", new { name = "North London" });

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "north london" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_EmptyName_Returns400()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/leagues", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 2: Add login helpers to the integration-test infrastructure**

Append to `tests/smash-dates.IntegrationTests/Infrastructure/HttpExtensions.cs`:

```csharp
public static async Task LoginAsAsync(this HttpClient client, string email, string password, TestDataSeeder seeder)
{
    await seeder.CreateUserAsync(email, password);
    var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
    response.EnsureSuccessStatusCode();
}

public static async Task LoginAsSystemAdminAsync(this HttpClient client, string email, string password, TestDataSeeder seeder)
{
    await seeder.CreateSystemAdminUserAsync(email, password);
    var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
    response.EnsureSuccessStatusCode();
}
```

Add to `TestDataSeeder`:

```csharp
public async Task<User> CreateSystemAdminUserAsync(string email, string password, string? displayName = null)
{
    var hash = _hasher.Hash(password);
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    var id = await conn.ExecuteScalarAsync<Guid>(
        @"INSERT INTO users (email, password_hash, display_name, is_active, is_system_admin)
          VALUES (lower(@email), @hash, @displayName, true, true)
          RETURNING id",
        new { email, hash, displayName });
    return new User
    {
        Id = id,
        Email = email.ToLowerInvariant(),
        PasswordHash = hash,
        DisplayName = displayName,
        IsActive = true,
        IsSystemAdmin = true,
    };
}
```

- [ ] **Step 3: Run, expect FAIL**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~CreateLeagueEndpointTests
```

Expected: 404 on `/api/leagues` — endpoint not registered.

- [ ] **Step 4: Write `LeagueEndpoints` group**

`Endpoints/Leagues/LeagueEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Leagues;

public static class LeagueEndpoints
{
    public static IEndpointRouteBuilder MapLeagueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues")
            .RequireAuthorization();

        group.MapCreateLeagueEndpoint();
        group.MapListLeaguesEndpoint();
        group.MapGetLeagueEndpoint();

        return app;
    }
}
```

- [ ] **Step 5: Write `CreateLeagueEndpoint`**

`Endpoints/Leagues/CreateLeagueEndpoint.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Leagues;

public static class CreateLeagueEndpoint
{
    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const string DuplicateNameSqlState = "23505";

    public sealed record CreateLeagueRequest(string Name, string? Description);
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
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim();
        if (description is { Length: > MaxDescriptionLength })
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Description too long");
        }

        var createdBy = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var id = await leagues.CreateAsync(name, description, createdBy, ct);
            return Results.Created($"/api/leagues/{id}", new LeagueResponse(id, name, description));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateNameSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "League name already in use");
        }
    }
}
```

- [ ] **Step 6: Write `ListLeaguesEndpoint`**

`Endpoints/Leagues/ListLeaguesEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Leagues;

public static class ListLeaguesEndpoint
{
    public sealed record LeagueSummary(Guid Id, string Name, string? Description);

    public static IEndpointRouteBuilder MapListLeaguesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(ILeagueRepository leagues, CancellationToken ct)
    {
        var rows = await leagues.ListAsync(ct);
        var summaries = rows.Select(l => new LeagueSummary(l.Id, l.Name, l.Description)).ToArray();
        return Results.Ok(summaries);
    }
}
```

- [ ] **Step 7: Write `GetLeagueEndpoint`**

`Endpoints/Leagues/GetLeagueEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Leagues;

public static class GetLeagueEndpoint
{
    public sealed record LeagueDetail(Guid Id, string Name, string? Description, Guid CreatedBy);

    public static IEndpointRouteBuilder MapGetLeagueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid id, ILeagueRepository leagues, CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(id, ct);
        return league is null
            ? Results.NotFound()
            : Results.Ok(new LeagueDetail(league.Id, league.Name, league.Description, league.CreatedBy));
    }
}
```

- [ ] **Step 8: Register repository + endpoints in `Program.cs`**

Add after `builder.Services.AddScoped<IUserRepository, UserRepository>();`:

```csharp
builder.Services.AddScoped<ILeagueRepository, LeagueRepository>();
```

Add after `app.MapAuthEndpoints();`:

```csharp
app.MapLeagueEndpoints();
```

Add `using smash_dates.Endpoints.Leagues;` at the top.

- [ ] **Step 9: Write list + get integration tests**

`tests/smash-dates.IntegrationTests/Endpoints/ListLeaguesEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using smash_dates.Endpoints.Leagues;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListLeaguesEndpointTests : IntegrationTestBase
{
    public ListLeaguesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/leagues");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Authenticated_ReturnsLeaguesSortedByName()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        await Seeder.CreateLeagueAsync("Beta", admin.Id);
        await Seeder.CreateLeagueAsync("Alpha", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync("/api/leagues");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListLeaguesEndpoint.LeagueSummary[]>();
        body!.Select(s => s.Name).Should().ContainInOrder("Alpha", "Beta");
    }
}
```

`tests/smash-dates.IntegrationTests/Endpoints/GetLeagueEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using smash_dates.Endpoints.Leagues;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class GetLeagueEndpointTests : IntegrationTestBase
{
    public GetLeagueEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ExistingLeague_Returns200()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var id = await Seeder.CreateLeagueAsync("North London", admin.Id, "desc");
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetLeagueEndpoint.LeagueDetail>();
        body!.Name.Should().Be("North London");
        body.Description.Should().Be("desc");
    }

    [Fact]
    public async Task Get_MissingLeague_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 10: Run all league endpoint tests, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~LeagueEndpointTests
```

- [ ] **Step 11: Commit**

```bash
git add Endpoints/Leagues Program.cs Repositories/ILeagueRepository.cs Repositories/LeagueRepository.cs tests/smash-dates.IntegrationTests/Endpoints/CreateLeagueEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/ListLeaguesEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/GetLeagueEndpointTests.cs tests/smash-dates.IntegrationTests/Infrastructure
git commit -m "feat(leagues): add create/list/get league endpoints"
```

---

### Task 8: Create `divisions` table migration

**Files:**
- Create: `Migrations/Scripts/0005_create_divisions.sql`

- [ ] **Step 1: Write the migration**

`Migrations/Scripts/0005_create_divisions.sql`:

```sql
CREATE TABLE divisions (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    league_id           uuid NOT NULL REFERENCES leagues(id) ON DELETE CASCADE,
    name                text NOT NULL,
    gender              text NOT NULL CHECK (gender IN ('Mens', 'Ladies', 'Mixed')),
    rank                integer NOT NULL,
    rubbers_per_match   integer NOT NULL CHECK (rubbers_per_match > 0),
    win_points          integer NOT NULL DEFAULT 2 CHECK (win_points >= 0),
    draw_points         integer NOT NULL DEFAULT 1 CHECK (draw_points >= 0),
    loss_points         integer NOT NULL DEFAULT 0 CHECK (loss_points >= 0),
    created_at          timestamptz NOT NULL DEFAULT now(),
    updated_at          timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_divisions_league_name_lower ON divisions (league_id, lower(name));
CREATE UNIQUE INDEX ux_divisions_league_gender_rank ON divisions (league_id, gender, rank);
```

- [ ] **Step 2: Run migrator tests, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~DbMigratorTests
```

- [ ] **Step 3: Commit**

```bash
git add Migrations/Scripts/0005_create_divisions.sql
git commit -m "feat(db): add divisions table"
```

---

### Task 9: `Division` model + repository

**Files:**
- Create: `Models/Division.cs`
- Create: `Models/DivisionGender.cs`
- Create: `Repositories/IDivisionRepository.cs`
- Create: `Repositories/DivisionRepository.cs`
- Create: `tests/smash-dates.IntegrationTests/Repositories/DivisionRepositoryTests.cs`
- Modify: `tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs`

- [ ] **Step 1: Write the gender enum**

`Models/DivisionGender.cs`:

```csharp
namespace smash_dates.Models;

public enum DivisionGender
{
    Mens,
    Ladies,
    Mixed,
}
```

- [ ] **Step 2: Write the `Division` model**

`Models/Division.cs`:

```csharp
namespace smash_dates.Models;

public sealed class Division
{
    public Guid Id { get; init; }
    public Guid LeagueId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DivisionGender Gender { get; init; }
    public int Rank { get; init; }
    public int RubbersPerMatch { get; init; }
    public int WinPoints { get; init; }
    public int DrawPoints { get; init; }
    public int LossPoints { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
```

Dapper stores enums as their string name when the column type is `text` if we configure it; the simpler path is to read/write the string explicitly. We'll handle this inside the repository by mapping to a string column.

- [ ] **Step 3: Write failing repository tests**

`tests/smash-dates.IntegrationTests/Repositories/DivisionRepositoryTests.cs`:

```csharp
using FluentAssertions;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;
using smash_dates.Repositories;

namespace smash_dates.IntegrationTests.Repositories;

public sealed class DivisionRepositoryTests : IntegrationTestBase
{
    public DivisionRepositoryTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateAsync_PersistsDivision()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var repo = new DivisionRepository(Fixture.ConnectionFactory);

        var id = await repo.CreateAsync(leagueId, "Mens 1", DivisionGender.Mens, rank: 1, rubbersPerMatch: 9, winPoints: 2, drawPoints: 1, lossPoints: 0);

        var loaded = await repo.GetByIdAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Mens 1");
        loaded.Gender.Should().Be(DivisionGender.Mens);
        loaded.Rank.Should().Be(1);
        loaded.RubbersPerMatch.Should().Be(9);
    }

    [Fact]
    public async Task ListByLeagueAsync_ReturnsDivisionsSortedByGenderThenRank()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var repo = new DivisionRepository(Fixture.ConnectionFactory);
        await repo.CreateAsync(leagueId, "Mens 2", DivisionGender.Mens, 2, 9, 2, 1, 0);
        await repo.CreateAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9, 2, 1, 0);
        await repo.CreateAsync(leagueId, "Ladies 1", DivisionGender.Ladies, 1, 6, 2, 1, 0);

        var results = await repo.ListByLeagueAsync(leagueId);

        results.Select(d => d.Name).Should().ContainInOrder("Ladies 1", "Mens 1", "Mens 2");
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        var repo = new DivisionRepository(Fixture.ConnectionFactory);
        await repo.CreateAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9, 2, 1, 0);

        Func<Task> act = () => repo.CreateAsync(leagueId, "MENS 1", DivisionGender.Mens, 99, 9, 2, 1, 0);

        await act.Should().ThrowAsync<Npgsql.PostgresException>();
    }
}
```

- [ ] **Step 4: Run, expect FAIL**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~DivisionRepositoryTests
```

- [ ] **Step 5: Write interface**

`Repositories/IDivisionRepository.cs`:

```csharp
using smash_dates.Models;

namespace smash_dates.Repositories;

public interface IDivisionRepository
{
    Task<Division?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Division>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default);
    Task<Guid> CreateAsync(
        Guid leagueId,
        string name,
        DivisionGender gender,
        int rank,
        int rubbersPerMatch,
        int winPoints,
        int drawPoints,
        int lossPoints,
        CancellationToken ct = default);
}
```

- [ ] **Step 6: Write repository**

`Repositories/DivisionRepository.cs`:

```csharp
using Dapper;
using smash_dates.Data;
using smash_dates.Models;

namespace smash_dates.Repositories;

public sealed class DivisionRepository : IDivisionRepository
{
    private const string SelectColumns =
        "id, league_id, name, gender, rank, rubbers_per_match, win_points, draw_points, loss_points, created_at, updated_at";

    private readonly IDbConnectionFactory _factory;

    public DivisionRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<Division?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<DivisionRow>(
            new CommandDefinition(
                $"SELECT {SelectColumns} FROM divisions WHERE id = @id",
                new { id },
                cancellationToken: ct));
        return row?.ToDivision();
    }

    public async Task<IReadOnlyList<Division>> ListByLeagueAsync(Guid leagueId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync<DivisionRow>(
            new CommandDefinition(
                $@"SELECT {SelectColumns} FROM divisions
                   WHERE league_id = @leagueId
                   ORDER BY gender, rank",
                new { leagueId },
                cancellationToken: ct));
        return rows.Select(r => r.ToDivision()).ToList();
    }

    public async Task<Guid> CreateAsync(
        Guid leagueId,
        string name,
        DivisionGender gender,
        int rank,
        int rubbersPerMatch,
        int winPoints,
        int drawPoints,
        int lossPoints,
        CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                @"INSERT INTO divisions
                  (league_id, name, gender, rank, rubbers_per_match, win_points, draw_points, loss_points)
                  VALUES (@leagueId, @name, @gender, @rank, @rubbersPerMatch, @winPoints, @drawPoints, @lossPoints)
                  RETURNING id",
                new
                {
                    leagueId,
                    name,
                    gender = gender.ToString(),
                    rank,
                    rubbersPerMatch,
                    winPoints,
                    drawPoints,
                    lossPoints,
                },
                cancellationToken: ct));
    }

    private sealed class DivisionRow
    {
        public Guid Id { get; init; }
        public Guid LeagueId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Gender { get; init; } = string.Empty;
        public int Rank { get; init; }
        public int RubbersPerMatch { get; init; }
        public int WinPoints { get; init; }
        public int DrawPoints { get; init; }
        public int LossPoints { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }

        public Division ToDivision() => new()
        {
            Id = Id,
            LeagueId = LeagueId,
            Name = Name,
            Gender = Enum.Parse<DivisionGender>(Gender),
            Rank = Rank,
            RubbersPerMatch = RubbersPerMatch,
            WinPoints = WinPoints,
            DrawPoints = DrawPoints,
            LossPoints = LossPoints,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };
    }
}
```

- [ ] **Step 7: Add seeder helper for divisions**

Append to `TestDataSeeder`:

```csharp
public async Task<Guid> CreateDivisionAsync(
    Guid leagueId,
    string name,
    DivisionGender gender,
    int rank,
    int rubbersPerMatch,
    int winPoints = 2,
    int drawPoints = 1,
    int lossPoints = 0)
{
    await using var conn = new NpgsqlConnection(_connectionString);
    await conn.OpenAsync();
    return await conn.ExecuteScalarAsync<Guid>(
        @"INSERT INTO divisions (league_id, name, gender, rank, rubbers_per_match, win_points, draw_points, loss_points)
          VALUES (@leagueId, @name, @gender, @rank, @rubbersPerMatch, @winPoints, @drawPoints, @lossPoints)
          RETURNING id",
        new { leagueId, name, gender = gender.ToString(), rank, rubbersPerMatch, winPoints, drawPoints, lossPoints });
}
```

- [ ] **Step 8: Run repo tests, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~DivisionRepositoryTests
```

- [ ] **Step 9: Commit**

```bash
git add Models/Division.cs Models/DivisionGender.cs Repositories/IDivisionRepository.cs Repositories/DivisionRepository.cs tests/smash-dates.IntegrationTests/Repositories/DivisionRepositoryTests.cs tests/smash-dates.IntegrationTests/Infrastructure/TestDataSeeder.cs
git commit -m "feat(divisions): add Division model and Dapper repository"
```

---

### Task 10: Division endpoints — create / list

**Files:**
- Create: `Endpoints/Divisions/DivisionEndpoints.cs`
- Create: `Endpoints/Divisions/CreateDivisionEndpoint.cs`
- Create: `Endpoints/Divisions/ListDivisionsEndpoint.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/CreateDivisionEndpointTests.cs`
- Create: `tests/smash-dates.IntegrationTests/Endpoints/ListDivisionsEndpointTests.cs`
- Modify: `Program.cs`

- [ ] **Step 1: Write failing create-division integration test**

`tests/smash-dates.IntegrationTests/Endpoints/CreateDivisionEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class CreateDivisionEndpointTests : IntegrationTestBase
{
    public CreateDivisionEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Post_AsSystemAdmin_CreatesDivision_Returns201()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

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

    [Fact]
    public async Task Post_UnknownLeague_Returns404()
    {
        await Client.LoginAsSystemAdminAsync("admin@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/leagues/{Guid.NewGuid()}/divisions", new
        {
            name = "Mens 1", gender = "Mens", rank = 1, rubbersPerMatch = 9, winPoints = 2, drawPoints = 1, lossPoints = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Post_InvalidGender_Returns400()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/divisions", new
        {
            name = "Mens 1", gender = "Vegan", rank = 1, rubbersPerMatch = 9, winPoints = 2, drawPoints = 1, lossPoints = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_AsNonAdmin_Returns403()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync($"/api/leagues/{leagueId}/divisions", new
        {
            name = "Mens 1", gender = "Mens", rank = 1, rubbersPerMatch = 9, winPoints = 2, drawPoints = 1, lossPoints = 0,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 2: Run, expect FAIL**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~CreateDivisionEndpointTests
```

- [ ] **Step 3: Write the endpoints group**

`Endpoints/Divisions/DivisionEndpoints.cs`:

```csharp
namespace smash_dates.Endpoints.Divisions;

public static class DivisionEndpoints
{
    public static IEndpointRouteBuilder MapDivisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leagues/{leagueId:guid}/divisions")
            .RequireAuthorization();

        group.MapCreateDivisionEndpoint();
        group.MapListDivisionsEndpoint();
        return app;
    }
}
```

- [ ] **Step 4: Write `CreateDivisionEndpoint`**

`Endpoints/Divisions/CreateDivisionEndpoint.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using smash_dates.Models;
using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Divisions;

public static class CreateDivisionEndpoint
{
    private const int MaxNameLength = 200;
    private const string DuplicateNameSqlState = "23505";

    public sealed record CreateDivisionRequest(
        string Name,
        string Gender,
        int Rank,
        int RubbersPerMatch,
        int WinPoints,
        int DrawPoints,
        int LossPoints);

    public sealed record DivisionResponse(
        Guid Id,
        Guid LeagueId,
        string Name,
        string Gender,
        int Rank,
        int RubbersPerMatch,
        int WinPoints,
        int DrawPoints,
        int LossPoints);

    public static IEndpointRouteBuilder MapCreateDivisionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle)
            .RequireAuthorization(AuthorizationPolicies.SystemAdmin);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        CreateDivisionRequest request,
        ILeagueRepository leagues,
        IDivisionRepository divisions,
        CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(leagueId, ct);
        if (league is null) return Results.NotFound();

        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || name.Length > MaxNameLength)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid name");

        if (!Enum.TryParse<DivisionGender>(request.Gender, ignoreCase: false, out var gender))
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid gender");

        if (request.Rank < 1)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Rank must be 1 or more");

        if (request.RubbersPerMatch <= 0)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "RubbersPerMatch must be positive");

        if (request.WinPoints < 0 || request.DrawPoints < 0 || request.LossPoints < 0)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Points must be non-negative");

        try
        {
            var id = await divisions.CreateAsync(
                leagueId, name, gender, request.Rank, request.RubbersPerMatch,
                request.WinPoints, request.DrawPoints, request.LossPoints, ct);

            return Results.Created($"/api/leagues/{leagueId}/divisions/{id}", new DivisionResponse(
                id, leagueId, name, gender.ToString(), request.Rank, request.RubbersPerMatch,
                request.WinPoints, request.DrawPoints, request.LossPoints));
        }
        catch (PostgresException ex) when (ex.SqlState == DuplicateNameSqlState)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Division already exists");
        }
    }
}
```

- [ ] **Step 5: Write `ListDivisionsEndpoint`**

`Endpoints/Divisions/ListDivisionsEndpoint.cs`:

```csharp
using smash_dates.Repositories;

namespace smash_dates.Endpoints.Divisions;

public static class ListDivisionsEndpoint
{
    public sealed record DivisionSummary(
        Guid Id,
        string Name,
        string Gender,
        int Rank,
        int RubbersPerMatch,
        int WinPoints,
        int DrawPoints,
        int LossPoints);

    public static IEndpointRouteBuilder MapListDivisionsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        Guid leagueId,
        ILeagueRepository leagues,
        IDivisionRepository divisions,
        CancellationToken ct)
    {
        var league = await leagues.GetByIdAsync(leagueId, ct);
        if (league is null) return Results.NotFound();

        var rows = await divisions.ListByLeagueAsync(leagueId, ct);
        var summaries = rows.Select(d => new DivisionSummary(
            d.Id, d.Name, d.Gender.ToString(), d.Rank, d.RubbersPerMatch,
            d.WinPoints, d.DrawPoints, d.LossPoints)).ToArray();
        return Results.Ok(summaries);
    }
}
```

- [ ] **Step 6: Register repository + endpoints in `Program.cs`**

Add `builder.Services.AddScoped<IDivisionRepository, DivisionRepository>();` next to the `ILeagueRepository` registration. Add `app.MapDivisionEndpoints();` after `app.MapLeagueEndpoints();`. Add `using smash_dates.Endpoints.Divisions;`.

- [ ] **Step 7: Write list tests**

`tests/smash-dates.IntegrationTests/Endpoints/ListDivisionsEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using smash_dates.Endpoints.Divisions;
using smash_dates.IntegrationTests.Infrastructure;
using smash_dates.Models;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ListDivisionsEndpointTests : IntegrationTestBase
{
    public ListDivisionsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Get_ReturnsDivisionsForLeague_OrderedByGenderThenRank()
    {
        var admin = await Seeder.CreateSystemAdminUserAsync("admin@example.com", "correct-horse-battery");
        var leagueId = await Seeder.CreateLeagueAsync("NL", admin.Id);
        await Seeder.CreateDivisionAsync(leagueId, "Mens 2", DivisionGender.Mens, 2, 9);
        await Seeder.CreateDivisionAsync(leagueId, "Mens 1", DivisionGender.Mens, 1, 9);
        await Seeder.CreateDivisionAsync(leagueId, "Ladies 1", DivisionGender.Ladies, 1, 6);
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.GetAsync($"/api/leagues/{leagueId}/divisions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListDivisionsEndpoint.DivisionSummary[]>();
        body!.Select(s => s.Name).Should().ContainInOrder("Ladies 1", "Mens 1", "Mens 2");
    }

    [Fact]
    public async Task Get_UnknownLeague_Returns404()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);
        var response = await Client.GetAsync($"/api/leagues/{Guid.NewGuid()}/divisions");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 8: Run all division endpoint tests, expect PASS**

```bash
dotnet test tests/smash-dates.IntegrationTests --filter FullyQualifiedName~DivisionEndpointTests
```

- [ ] **Step 9: Commit**

```bash
git add Endpoints/Divisions Program.cs tests/smash-dates.IntegrationTests/Endpoints/CreateDivisionEndpointTests.cs tests/smash-dates.IntegrationTests/Endpoints/ListDivisionsEndpointTests.cs
git commit -m "feat(divisions): add create/list division endpoints"
```

---

### Task 11: Surface `isSystemAdmin` in Angular auth store

**Files:**
- Modify: `ClientApp/src/app/core/auth/user.model.ts`
- Modify: `ClientApp/src/app/core/auth/auth.store.ts`
- Modify: `ClientApp/src/app/core/auth/auth.api.ts` (if it parses the user payload)

- [ ] **Step 1: Inspect existing user model**

```bash
type ClientApp\src\app\core\auth\user.model.ts
```

(Or read it through the IDE.) The current shape is roughly `{ id, email, displayName }`. Add `isSystemAdmin: boolean`.

- [ ] **Step 2: Update the TypeScript model**

`ClientApp/src/app/core/auth/user.model.ts`:

```typescript
export interface User {
  id: string;
  email: string;
  displayName: string | null;
  isSystemAdmin: boolean;
}
```

- [ ] **Step 3: Verify the store / API parse the new field**

If `auth.api.ts` declares an explicit DTO type, add `isSystemAdmin` to it. If the store has selectors, add `selectIsSystemAdmin = computed(() => state().user?.isSystemAdmin ?? false);` (or signal equivalent, matching existing idiom).

- [ ] **Step 4: Update the auth-store unit test if one exists**

In `ClientApp/src/app/core/auth/auth.store.spec.ts`, extend the fake user used in existing tests to include `isSystemAdmin: false`. Add a new test that confirms the store exposes the flag.

- [ ] **Step 5: Run frontend tests**

```bash
cd ClientApp
npm test -- --watch=false
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add ClientApp/src/app/core/auth
git commit -m "feat(client): expose isSystemAdmin on the User model"
```

---

### Task 12: Angular `systemAdminGuard`

**Files:**
- Create: `ClientApp/src/app/core/auth/system-admin.guard.ts`
- Modify: `ClientApp/src/app/app.routes.ts`

- [ ] **Step 1: Write the guard**

`ClientApp/src/app/core/auth/system-admin.guard.ts`:

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

export const systemAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  if (auth.user()?.isSystemAdmin) {
    return true;
  }
  return router.createUrlTree(['/']);
};
```

(Adjust signal accessor names to match the existing `AuthStore` API — `auth.user()` if signal, `auth.user$` if observable, etc. Inspect first.)

- [ ] **Step 2: Add an admin route in `app.routes.ts`**

Append:

```typescript
{
  path: 'admin',
  canActivate: [authGuard, systemAdminGuard],
  loadChildren: () =>
    import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
},
```

- [ ] **Step 3: Run frontend tests**

```bash
cd ClientApp
npm test -- --watch=false
```

Expected: PASS (no new tests yet; just no regressions).

- [ ] **Step 4: Commit**

```bash
git add ClientApp/src/app/core/auth/system-admin.guard.ts ClientApp/src/app/app.routes.ts
git commit -m "feat(client): add systemAdminGuard and admin lazy route"
```

---

### Task 13: Angular leagues API client

**Files:**
- Create: `ClientApp/src/app/features/admin/leagues.api.ts`

- [ ] **Step 1: Write the API client**

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface LeagueSummary {
  id: string;
  name: string;
  description: string | null;
}

export interface LeagueDetail extends LeagueSummary {
  createdBy: string;
}

export interface DivisionSummary {
  id: string;
  name: string;
  gender: 'Mens' | 'Ladies' | 'Mixed';
  rank: number;
  rubbersPerMatch: number;
  winPoints: number;
  drawPoints: number;
  lossPoints: number;
}

export interface CreateLeagueRequest {
  name: string;
  description?: string | null;
}

export interface CreateDivisionRequest {
  name: string;
  gender: 'Mens' | 'Ladies' | 'Mixed';
  rank: number;
  rubbersPerMatch: number;
  winPoints: number;
  drawPoints: number;
  lossPoints: number;
}

@Injectable({ providedIn: 'root' })
export class LeaguesApi {
  private readonly http = inject(HttpClient);

  list(): Observable<LeagueSummary[]> {
    return this.http.get<LeagueSummary[]>('/api/leagues');
  }

  get(id: string): Observable<LeagueDetail> {
    return this.http.get<LeagueDetail>(`/api/leagues/${id}`);
  }

  create(req: CreateLeagueRequest): Observable<LeagueSummary> {
    return this.http.post<LeagueSummary>('/api/leagues', req);
  }

  listDivisions(leagueId: string): Observable<DivisionSummary[]> {
    return this.http.get<DivisionSummary[]>(`/api/leagues/${leagueId}/divisions`);
  }

  createDivision(leagueId: string, req: CreateDivisionRequest): Observable<DivisionSummary> {
    return this.http.post<DivisionSummary>(`/api/leagues/${leagueId}/divisions`, req);
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add ClientApp/src/app/features/admin/leagues.api.ts
git commit -m "feat(client): add LeaguesApi http client"
```

---

### Task 14: Angular leagues list page

**Files:**
- Create: `ClientApp/src/app/features/admin/leagues-list.page.ts`
- Create: `ClientApp/src/app/features/admin/admin.routes.ts`

- [ ] **Step 1: Write the routes file**

`ClientApp/src/app/features/admin/admin.routes.ts`:

```typescript
import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'leagues',
  },
  {
    path: 'leagues',
    loadComponent: () =>
      import('./leagues-list.page').then((m) => m.LeaguesListPage),
  },
  {
    path: 'leagues/:id',
    loadComponent: () =>
      import('./league-detail.page').then((m) => m.LeagueDetailPage),
  },
];
```

- [ ] **Step 2: Write the leagues list page**

`ClientApp/src/app/features/admin/leagues-list.page.ts`:

```typescript
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LeaguesApi, LeagueSummary } from './leagues.api';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="admin-page">
      <header><h1>Leagues</h1></header>

      <form (ngSubmit)="onCreate()" class="admin-form">
        <label>
          Name
          <input name="name" [(ngModel)]="newName" required />
        </label>
        <label>
          Description
          <input name="description" [(ngModel)]="newDescription" />
        </label>
        <button type="submit" [disabled]="submitting()">Create league</button>
        <p *ngIf="error()" class="error">{{ error() }}</p>
      </form>

      <ul class="leagues">
        <li *ngFor="let l of leagues()">
          <a [routerLink]="['/admin/leagues', l.id]">{{ l.name }}</a>
          <span *ngIf="l.description"> — {{ l.description }}</span>
        </li>
      </ul>
    </section>
  `,
  styles: [
    `.admin-page { padding: 2rem; font-family: 'JetBrains Mono', monospace; }`,
    `.admin-form { display: grid; gap: 0.5rem; margin: 1rem 0; }`,
    `.error { color: #ef4444; }`,
  ],
})
export class LeaguesListPage {
  private readonly api = inject(LeaguesApi);

  readonly leagues = signal<LeagueSummary[]>([]);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  newName = '';
  newDescription = '';

  constructor() {
    this.refresh();
  }

  refresh(): void {
    this.api.list().subscribe({
      next: (rows) => this.leagues.set(rows),
      error: () => this.error.set('Failed to load leagues.'),
    });
  }

  onCreate(): void {
    if (!this.newName.trim()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.api.create({ name: this.newName.trim(), description: this.newDescription.trim() || null }).subscribe({
      next: () => {
        this.newName = '';
        this.newDescription = '';
        this.submitting.set(false);
        this.refresh();
      },
      error: (err) => {
        this.submitting.set(false);
        this.error.set(err?.error?.title ?? 'Create failed.');
      },
    });
  }
}
```

- [ ] **Step 3: Build the client to surface compile errors**

```bash
cd ClientApp
npm run build
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add ClientApp/src/app/features/admin/admin.routes.ts ClientApp/src/app/features/admin/leagues-list.page.ts
git commit -m "feat(client): leagues list + create page"
```

---

### Task 15: Angular league detail page (with divisions)

**Files:**
- Create: `ClientApp/src/app/features/admin/league-detail.page.ts`

- [ ] **Step 1: Write the page**

```typescript
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { switchMap, tap } from 'rxjs';
import { CreateDivisionRequest, DivisionSummary, LeagueDetail, LeaguesApi } from './leagues.api';

@Component({
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="admin-page">
      <header *ngIf="league() as l"><h1>{{ l.name }}</h1><p *ngIf="l.description">{{ l.description }}</p></header>

      <h2>Divisions</h2>
      <ul>
        <li *ngFor="let d of divisions()">
          {{ d.name }} — {{ d.gender }} #{{ d.rank }} ·
          rubbers/match {{ d.rubbersPerMatch }} ·
          points {{ d.winPoints }}/{{ d.drawPoints }}/{{ d.lossPoints }}
        </li>
      </ul>

      <form (ngSubmit)="onCreate()" class="admin-form">
        <label>Name <input name="name" [(ngModel)]="form.name" required /></label>
        <label>Gender
          <select name="gender" [(ngModel)]="form.gender">
            <option value="Mens">Mens</option>
            <option value="Ladies">Ladies</option>
            <option value="Mixed">Mixed</option>
          </select>
        </label>
        <label>Rank <input type="number" name="rank" [(ngModel)]="form.rank" min="1" /></label>
        <label>Rubbers per match
          <input type="number" name="rubbersPerMatch" [(ngModel)]="form.rubbersPerMatch" min="1" />
        </label>
        <label>Win pts <input type="number" name="winPoints" [(ngModel)]="form.winPoints" /></label>
        <label>Draw pts <input type="number" name="drawPoints" [(ngModel)]="form.drawPoints" /></label>
        <label>Loss pts <input type="number" name="lossPoints" [(ngModel)]="form.lossPoints" /></label>
        <button type="submit">Add division</button>
        <p *ngIf="error()" class="error">{{ error() }}</p>
      </form>
    </section>
  `,
  styles: [
    `.admin-page { padding: 2rem; font-family: 'JetBrains Mono', monospace; }`,
    `.admin-form { display: grid; gap: 0.5rem; margin: 1rem 0; }`,
    `.error { color: #ef4444; }`,
  ],
})
export class LeagueDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(LeaguesApi);

  readonly league = signal<LeagueDetail | null>(null);
  readonly divisions = signal<DivisionSummary[]>([]);
  readonly error = signal<string | null>(null);
  private leagueId = '';

  form: CreateDivisionRequest = {
    name: '',
    gender: 'Mens',
    rank: 1,
    rubbersPerMatch: 9,
    winPoints: 2,
    drawPoints: 1,
    lossPoints: 0,
  };

  constructor() {
    this.route.paramMap.pipe(
      tap((p) => (this.leagueId = p.get('id')!)),
      switchMap((p) => this.api.get(p.get('id')!)),
      tap((l) => this.league.set(l)),
      switchMap((l) => this.api.listDivisions(l.id)),
    ).subscribe({
      next: (rows) => this.divisions.set(rows),
      error: () => this.error.set('Failed to load.'),
    });
  }

  onCreate(): void {
    this.api.createDivision(this.leagueId, { ...this.form }).subscribe({
      next: () => {
        this.form = { name: '', gender: 'Mens', rank: 1, rubbersPerMatch: 9, winPoints: 2, drawPoints: 1, lossPoints: 0 };
        this.api.listDivisions(this.leagueId).subscribe((rows) => this.divisions.set(rows));
      },
      error: (err) => this.error.set(err?.error?.title ?? 'Create failed.'),
    });
  }
}
```

- [ ] **Step 2: Build the client**

```bash
cd ClientApp
npm run build
```

Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add ClientApp/src/app/features/admin/league-detail.page.ts
git commit -m "feat(client): league detail page with divisions"
```

---

### Task 16: README + smoke

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add a foundation section**

Append (or insert under an "Architecture" heading) a section like:

```markdown
## Foundation slice

The first vertical slice exposes:

- `POST /api/auth/register` — first registered user is promoted to **SystemAdmin**.
- `POST /api/leagues` *(SystemAdmin)* — create a League.
- `GET  /api/leagues` *(authenticated)* — list Leagues.
- `GET  /api/leagues/{id}` *(authenticated)* — get one League.
- `POST /api/leagues/{leagueId}/divisions` *(SystemAdmin)* — create a Division.
- `GET  /api/leagues/{leagueId}/divisions` *(authenticated)* — list Divisions.

Frontend: `/admin/leagues` (list + create) and `/admin/leagues/:id` (detail + divisions). Visible only to SystemAdmins.

Subsequent slices add Clubs, Teams, Venues, Seasons, Weeks, Blocked Dates, role grants for LeagueAdmin/ClubAdmin, then the scheduler.
```

- [ ] **Step 2: Run the full test suite**

```bash
dotnet test
```

Expected: PASS.

- [ ] **Step 3: Manual smoke test**

```bash
docker compose up -d postgres
dotnet run --project smash-dates.csproj
```

In a second shell:

```bash
curl -i -X POST http://localhost:5000/api/auth/register \
  -H "content-type: application/json" \
  -d '{"email":"me@example.com","password":"correct-horse-battery"}'

curl -i -X POST http://localhost:5000/api/leagues \
  --cookie cookies.txt --cookie-jar cookies.txt \
  -H "content-type: application/json" \
  -d '{"name":"North London","description":"Top division"}'
```

(Use `--cookie-jar` on the register call to capture the auth cookie first; reuse the jar on subsequent calls.)

Expected: register returns 200 with `"isSystemAdmin": true`, league create returns 201.

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs(readme): document foundation slice endpoints"
```

---

## Self-Review

**Spec coverage:**
- SystemAdmin bootstrap ✓ (Task 3)
- Authorization policy ✓ (Task 4)
- League CRUD-ish (create / list / get; no update/delete intentionally — YAGNI for v1) ✓ (Tasks 5-7)
- Division CRUD-ish (create / list; no update/delete yet) ✓ (Tasks 8-10)
- Per-Division `RubbersPerMatch` + `PointsScheme` ✓ (Tasks 8-10)
- Per-Division gender enum ✓ (Tasks 9, 10)
- Frontend admin shell ✓ (Tasks 11-15)
- README ✓ (Task 16)
- Tests at repository + endpoint level ✓
- All migrations follow existing numeric prefix convention ✓

**Deferred (covered by later plans):**
- Update / delete endpoints for League and Division — deliberately omitted; first slice only proves create/read.
- LeagueAdmin role grant — first slice uses SystemAdmin for everything; LeagueAdmin lands when Clubs do.
- Brutalist visual polish — page styles are functional only; the Court Geometry Brutalist treatment lands once the frontend has more than two pages to justify shared layout/styling work.

**Placeholder scan:** none.

**Type consistency:** `IsSystemAdmin` flag propagated through `User`, `UserResponse`, `MeEndpoint`, frontend `User`. `DivisionGender` enum string-mapped at both ends consistently (`"Mens" | "Ladies" | "Mixed"`).
