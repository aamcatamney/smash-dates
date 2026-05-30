using smash_dates.Services.Standings;

namespace smash_dates.UnitTests.Services.Standings;

public sealed class StandingsCalculatorTests
{
    private static readonly PointsScheme Default = new(2, 1, 0);

    private static StandingTeam Team(string name, out Guid id)
    {
        id = Guid.NewGuid();
        return new StandingTeam(id, name);
    }

    [Fact]
    public void NoResults_AllTeamsZeroed_OrderedByName()
    {
        var a = new StandingTeam(Guid.NewGuid(), "Bravo");
        var b = new StandingTeam(Guid.NewGuid(), "Alpha");

        var rows = StandingsCalculator.Compute([a, b], Default, []);

        rows.Should().HaveCount(2);
        rows[0].TeamName.Should().Be("Alpha");
        rows.Should().OnlyContain(r => r.Played == 0 && r.Points == 0);
    }

    [Fact]
    public void Win_AwardsWinPointsAndRubbers()
    {
        var a = Team("A", out var aId);
        var b = Team("B", out var bId);

        var rows = StandingsCalculator.Compute([a, b], Default, [new StandingResult(aId, bId, 9, 0)]);

        var winner = rows.Single(r => r.TeamId == aId);
        winner.Played.Should().Be(1);
        winner.Won.Should().Be(1);
        winner.RubbersFor.Should().Be(9);
        winner.RubbersAgainst.Should().Be(0);
        winner.RubberDifference.Should().Be(9);
        winner.Points.Should().Be(2);

        var loser = rows.Single(r => r.TeamId == bId);
        loser.Lost.Should().Be(1);
        loser.RubberDifference.Should().Be(-9);
        loser.Points.Should().Be(0);
    }

    [Fact]
    public void Draw_AwardsDrawPointsToBoth()
    {
        var a = Team("A", out var aId);
        var b = Team("B", out var bId);

        var rows = StandingsCalculator.Compute([a, b], Default, [new StandingResult(aId, bId, 3, 3)]);

        rows.Should().OnlyContain(r => r.Drawn == 1 && r.Points == 1 && r.RubberDifference == 0);
    }

    [Fact]
    public void Sorts_ByPoints_ThenRubberDiff_ThenRubbersFor_ThenName()
    {
        var a = Team("A", out var aId);
        var b = Team("B", out var bId);
        var c = Team("C", out var cId);
        var d = Team("D", out var dId);

        // a and b both win 9-0 (tie on points, diff, rubbers-for) → name breaks it (A before B).
        // c and d both lose 0-9 (identical) → name breaks it (C before D).
        var results = new[]
        {
            new StandingResult(aId, cId, 9, 0),
            new StandingResult(bId, dId, 9, 0),
        };

        var rows = StandingsCalculator.Compute([a, b, c, d], Default, results);

        rows.Select(r => r.TeamId).Should().ContainInOrder(aId, bId, cId, dId);
    }

    [Fact]
    public void AggregatesMultipleMatches()
    {
        var a = Team("A", out var aId);
        var b = Team("B", out var bId);

        var rows = StandingsCalculator.Compute([a, b], Default,
        [
            new StandingResult(aId, bId, 9, 0),
            new StandingResult(bId, aId, 4, 5), // a wins away too
        ]);

        var aRow = rows.Single(r => r.TeamId == aId);
        aRow.Played.Should().Be(2);
        aRow.Won.Should().Be(2);
        aRow.RubbersFor.Should().Be(14);
        aRow.RubbersAgainst.Should().Be(4);
        aRow.Points.Should().Be(4);
    }
}
