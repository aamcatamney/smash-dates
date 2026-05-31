using Microsoft.Extensions.DependencyInjection;
using smash_dates.Repositories;
using smash_dates.IntegrationTests.Infrastructure;

namespace smash_dates.IntegrationTests.Repositories;

// Auth token lifecycle: single-use, latest-link-wins on re-issue, and pruning of spent tokens.
public sealed class AuthTokenRepositoryTests : IntegrationTestBase
{
    public AuthTokenRepositoryTests(PostgresFixture fixture) : base(fixture) { }

    private const string Reset = "PasswordReset";
    private const string Verify = "EmailVerification";

    private async Task<(IAuthTokenRepository repo, Guid userId)> SetupAsync()
    {
        var user = await Seeder.CreateUserAsync("u@example.com", "correct-horse-battery");
        var scope = Factory.Services.CreateScope();
        return (scope.ServiceProvider.GetRequiredService<IAuthTokenRepository>(), user.Id);
    }

    [Fact]
    public async Task Issue_InvalidatesPriorUnusedTokenOfSamePurpose()
    {
        var (repo, userId) = await SetupAsync();

        var first = await repo.IssueAsync(userId, Reset, TimeSpan.FromHours(1));
        var second = await repo.IssueAsync(userId, Reset, TimeSpan.FromHours(1));

        (await repo.ConsumeAsync(first, Reset)).Should().BeNull("re-issuing supersedes the older link");
        (await repo.ConsumeAsync(second, Reset)).Should().Be(userId);
    }

    [Fact]
    public async Task Issue_DoesNotInvalidateTokensOfADifferentPurpose()
    {
        var (repo, userId) = await SetupAsync();

        var reset = await repo.IssueAsync(userId, Reset, TimeSpan.FromHours(1));
        await repo.IssueAsync(userId, Verify, TimeSpan.FromDays(7));

        (await repo.ConsumeAsync(reset, Reset)).Should().Be(userId, "a different purpose is untouched");
    }

    [Fact]
    public async Task Consume_IsSingleUse()
    {
        var (repo, userId) = await SetupAsync();
        var token = await repo.IssueAsync(userId, Reset, TimeSpan.FromHours(1));

        (await repo.ConsumeAsync(token, Reset)).Should().Be(userId);
        (await repo.ConsumeAsync(token, Reset)).Should().BeNull("a consumed token can't be reused");
    }

    [Fact]
    public async Task DeleteSpent_RemovesUsedAndExpired_KeepsLive()
    {
        var (repo, userId) = await SetupAsync();

        var used = await repo.IssueAsync(userId, Reset, TimeSpan.FromHours(1));
        await repo.ConsumeAsync(used, Reset);                       // spent: used
        await repo.IssueAsync(userId, Verify, TimeSpan.FromSeconds(-1)); // spent: already expired
        var live = await repo.IssueAsync(userId, Reset, TimeSpan.FromHours(1));

        var removed = await repo.DeleteSpentAsync();

        removed.Should().Be(2);
        (await repo.ConsumeAsync(live, Reset)).Should().Be(userId, "the live token survives the prune");
    }
}
