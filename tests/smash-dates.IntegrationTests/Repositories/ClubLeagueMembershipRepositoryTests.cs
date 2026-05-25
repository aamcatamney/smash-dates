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
