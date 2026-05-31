using smash_dates.Models;
using smash_dates.Services.Pegboard;

namespace smash_dates.UnitTests.Services.Pegboard;

public class GameMakeupTests
{
    [Fact]
    public void Singles_NeedsOnePerSide()
        => GameMakeup.SideSize(GameType.Singles).Should().Be(1);

    [Theory]
    [InlineData(GameType.Doubles)]
    [InlineData(GameType.Mixed)]
    [InlineData(GameType.Funny)]
    public void FourPlayerTypes_NeedTwoPerSide(GameType type)
        => GameMakeup.SideSize(type).Should().Be(2);

    [Fact]
    public void Doubles_AllSameGender_IsValid()
        => GameMakeup.IsValid(GameType.Doubles,
            [Gender.Male, Gender.Male], [Gender.Male, Gender.Male]).Should().BeTrue();

    [Fact]
    public void Doubles_MixedGenders_IsInvalid()
        => GameMakeup.IsValid(GameType.Doubles,
            [Gender.Male, Gender.Female], [Gender.Male, Gender.Male]).Should().BeFalse();

    [Fact]
    public void Mixed_EachSideOneMaleOneFemale_IsValid()
        => GameMakeup.IsValid(GameType.Mixed,
            [Gender.Male, Gender.Female], [Gender.Male, Gender.Female]).Should().BeTrue();

    [Fact]
    public void Mixed_SideOfTwoMales_IsInvalid()
        => GameMakeup.IsValid(GameType.Mixed,
            [Gender.Male, Gender.Male], [Gender.Female, Gender.Female]).Should().BeFalse();

    [Fact]
    public void Funny_AnyFourPlayerArrangement_IsValid()
        => GameMakeup.IsValid(GameType.Funny,
            [Gender.Male, Gender.Male], [Gender.Female, Gender.Female]).Should().BeTrue();

    [Fact]
    public void WrongCount_IsInvalid()
        => GameMakeup.IsValid(GameType.Singles,
            [Gender.Male, Gender.Male], [Gender.Female]).Should().BeFalse();
}
