using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ImportClubsEndpointTests : IntegrationTestBase
{
    public ImportClubsEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record RowErrorDto(int Row, string Message);
    private sealed record ImportResultDto(int Created, int Updated, RowErrorDto[] Errors);
    private sealed record ClubDto(Guid Id, string Name, string ShortCode, string ContactEmail);

    [Fact]
    public async Task Import_NewClubWithRegisteredAdmin_Creates()
    {
        await Seeder.CreateUserAsync("tvb-admin@example.com", "correct-horse-battery");
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/clubs/import", new
        {
            csv = "name,shortCode,contactEmail,firstAdminEmail\nThames Valley,TVB,info@tv.test,tvb-admin@example.com",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(1);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_UnknownAdminEmail_ReportsRowError()
    {
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/clubs/import", new
        {
            csv = "name,shortCode,contactEmail,firstAdminEmail\nThames Valley,TVB,info@tv.test,ghost@nope.test",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(0);
        result.Errors.Should().ContainSingle(e => e.Message.Contains("ghost@nope.test"));
    }

    [Fact]
    public async Task Import_ExistingShortCode_UpdatesNameAndEmail()
    {
        await Client.LoginAsSystemAdminAsync("sys@example.com", "correct-horse-battery", Seeder);
        await Seeder.CreateClubAsync("Old Name", "TVB", contactEmail: "old@tv.test");

        var response = await Client.PostAsJsonAsync("/api/clubs/import", new
        {
            csv = "name,shortCode,contactEmail,firstAdminEmail\nThames Valley,TVB,new@tv.test,",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Updated.Should().Be(1);
        result.Created.Should().Be(0);

        var clubs = await Client.GetFromJsonAsync<ClubDto[]>("/api/clubs");
        var tvb = clubs!.Single(c => c.ShortCode == "TVB");
        tvb.Name.Should().Be("Thames Valley");
        tvb.ContactEmail.Should().Be("new@tv.test");
    }

    [Fact]
    public async Task Import_AsNonSystemAdmin_Returns403()
    {
        await Client.LoginAsAsync("plain@example.com", "correct-horse-battery", Seeder);

        var response = await Client.PostAsJsonAsync("/api/clubs/import", new
        {
            csv = "name,shortCode,contactEmail,firstAdminEmail\nThames Valley,TVB,info@tv.test,x@y.test",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
