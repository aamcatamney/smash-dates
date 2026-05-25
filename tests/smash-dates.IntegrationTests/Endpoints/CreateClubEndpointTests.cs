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
