using smash_dates.Models;

namespace smash_dates.Services.Pegboard;

/// Per-side player counts and gender-makeup rules per game type. Funny is the
/// catch-all 4-player type and is always considered a valid makeup.
public static class GameMakeup
{
    public static int SideSize(GameType type) => type == GameType.Singles ? 1 : 2;

    public static bool IsValid(GameType type, IReadOnlyList<Gender> sideA, IReadOnlyList<Gender> sideB)
    {
        var size = SideSize(type);
        if (sideA.Count != size || sideB.Count != size) return false;

        return type switch
        {
            GameType.Singles => true,
            GameType.Funny => true,
            GameType.Doubles => sideA.Concat(sideB).Distinct().Count() == 1, // all four one gender
            GameType.Mixed => OneEach(sideA) && OneEach(sideB),
            _ => false,
        };
    }

    private static bool OneEach(IReadOnlyList<Gender> side)
        => side.Count == 2 && side[0] != side[1];
}
