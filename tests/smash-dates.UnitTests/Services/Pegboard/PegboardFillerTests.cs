using smash_dates.Models;
using smash_dates.Services.Pegboard;

namespace smash_dates.UnitTests.Services.Pegboard;

public class PegboardFillerTests
{
    private static FillCandidate C(string id, Gender g, int? grade, int order)
        => new(Guid.Parse(id.PadLeft(32, '0').Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-")),
               g, grade, order);

    [Fact]
    public void Mixed_FormsOneMaleOneFemalePerSide()
    {
        var pool = new[]
        {
            C("1", Gender.Male, 3, 0),
            C("2", Gender.Female, 3, 1),
            C("3", Gender.Male, 3, 2),
            C("4", Gender.Female, 3, 3),
        };
        var result = PegboardFiller.Suggest(GameType.Mixed, pool, playedPairs: []);
        result.Should().NotBeNull();
        result!.SideA.Should().HaveCount(2);
        result.SideB.Should().HaveCount(2);
        // each side must be 1M+1F
        SideGenders(result.SideA, pool).Should().BeEquivalentTo([Gender.Male, Gender.Female]);
        SideGenders(result.SideB, pool).Should().BeEquivalentTo([Gender.Male, Gender.Female]);
    }

    [Fact]
    public void Mixed_NotEnoughOfAGender_ReturnsNull()
    {
        var pool = new[]
        {
            C("1", Gender.Male, 3, 0),
            C("2", Gender.Male, 3, 1),
            C("3", Gender.Male, 3, 2),
            C("4", Gender.Female, 3, 3),
        };
        PegboardFiller.Suggest(GameType.Mixed, pool, playedPairs: []).Should().BeNull();
    }

    [Fact]
    public void Singles_PrefersLongestWaiting()
    {
        var pool = new[]
        {
            C("1", Gender.Male, 3, 0),
            C("2", Gender.Male, 3, 1),
            C("3", Gender.Male, 3, 2),
        };
        var result = PegboardFiller.Suggest(GameType.Singles, pool, playedPairs: []);
        var chosen = result!.SideA.Concat(result.SideB).ToHashSet();
        chosen.Should().Contain(pool[0].Id);
        chosen.Should().Contain(pool[1].Id);
    }

    private static List<Gender> SideGenders(IReadOnlyList<Guid> side, FillCandidate[] pool)
        => side.Select(id => pool.Single(p => p.Id == id).Gender).ToList();
}
