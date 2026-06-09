using System.Net;
using System.Net.Http.Json;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Endpoints;

public sealed class ImportVenuesEndpointTests : IntegrationTestBase
{
    public ImportVenuesEndpointTests(PostgresFixture fixture) : base(fixture) { }

    private sealed record RowErrorDto(int Row, string Message);
    private sealed record ImportResultDto(int Created, int Updated, RowErrorDto[] Errors);
    private sealed record VenueDto(Guid Id, string Name, int Courts, int MaxConcurrentMatches);

    private async Task<Guid> ArrangeClubWithAdminAsync()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });
        return clubId;
    }

    [Fact]
    public async Task Import_WithAddressColumn_StoresAddress()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/venues/import", new
        {
            csv = "name,courts,maxConcurrentMatches,address\nMain Hall,4,2,12 High St\nAnnexe,2,1,",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(2);
        var venues = await Client.GetFromJsonAsync<List<AddressedVenueDto>>($"/api/clubs/{clubId}/venues");
        venues!.Single(v => v.Name == "Main Hall").Address.Should().Be("12 High St");
        venues.Single(v => v.Name == "Annexe").Address.Should().BeNull();
    }

    private sealed record AddressedVenueDto(Guid Id, string Name, string? Address);

    [Fact]
    public async Task Import_NewVenues_Creates()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/venues/import", new
        {
            csv = "name,courts,maxConcurrentMatches\nMain Hall,4,2\nAnnexe,2,1",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(2);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Import_ExistingVenue_UpdatesCourtsAndMaxConcurrent()
    {
        var admin = await Seeder.CreateUserAsync("admin@example.com", "correct-horse-battery");
        var clubId = await Seeder.CreateClubAsync("Acme", "ACME");
        await Seeder.GrantClubAdminAsync(clubId, admin.Id, admin.Id);
        await Seeder.CreateVenueAsync(clubId, "Main Hall", 1);
        await Client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.com", password = "correct-horse-battery" });

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/venues/import", new
        {
            csv = "name,courts,maxConcurrentMatches\nMain Hall,6,2\nAnnexe,2,1",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(1);
        result.Updated.Should().Be(1);

        var venues = await Client.GetFromJsonAsync<VenueDto[]>($"/api/clubs/{clubId}/venues");
        var main = venues!.Single(v => v.Name == "Main Hall");
        main.Courts.Should().Be(6);
        main.MaxConcurrentMatches.Should().Be(2);
    }

    [Fact]
    public async Task Import_InvalidMaxConcurrent_ReportsRowError()
    {
        var clubId = await ArrangeClubWithAdminAsync();

        var response = await Client.PostAsJsonAsync($"/api/clubs/{clubId}/venues/import", new
        {
            csv = "name,courts,maxConcurrentMatches\nMain Hall,4,3\nAnnexe,2,1",
        });

        var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
        result!.Created.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Row == 2);
    }
}
