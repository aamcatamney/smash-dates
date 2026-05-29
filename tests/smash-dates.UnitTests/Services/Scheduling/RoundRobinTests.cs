using smash_dates.Services.Scheduling;

namespace smash_dates.UnitTests.Services.Scheduling;

public sealed class RoundRobinTests
{
    [Fact]
    public void DoubleRoundRobin_FourTeams_ProducesEveryOrderedPairOnce()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();

        var pairs = RoundRobin.DoubleRoundRobin([a, b, c, d]);

        // N*(N-1) ordered pairs for a double round-robin.
        pairs.Count.Should().Be(12);
        pairs.Should().OnlyHaveUniqueItems();
        foreach (var home in new[] { a, b, c, d })
        {
            foreach (var away in new[] { a, b, c, d })
            {
                if (home == away) continue;
                pairs.Should().Contain((home, away));
            }
        }
    }

    [Fact]
    public void DoubleRoundRobin_TwoTeams_PlaysHomeAndAway()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var pairs = RoundRobin.DoubleRoundRobin([a, b]);

        pairs.Count.Should().Be(2);
        pairs.Should().Contain((a, b));
        pairs.Should().Contain((b, a));
    }

    [Fact]
    public void DoubleRoundRobin_FewerThanTwoTeams_ProducesNothing()
    {
        RoundRobin.DoubleRoundRobin([Guid.NewGuid()]).Should().BeEmpty();
        RoundRobin.DoubleRoundRobin([]).Should().BeEmpty();
    }

    [Fact]
    public void DoubleRoundRobin_NoTeamPlaysItself()
    {
        var teams = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();

        var pairs = RoundRobin.DoubleRoundRobin(teams);

        pairs.Count.Should().Be(30);
        pairs.Should().NotContain(p => p.Home == p.Away);
    }
}
