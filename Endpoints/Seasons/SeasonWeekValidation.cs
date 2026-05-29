using smash_dates.Models;

namespace smash_dates.Endpoints.Seasons;

// Wire format for a single Week in create / replace-weeks request bodies.
public sealed record WeekInput(DateOnly StartDate, DateOnly EndDate, string WeekType);

public sealed record ValidatedWeek(DateOnly StartDate, DateOnly EndDate, WeekType WeekType);

public static class SeasonWeekValidation
{
    // Returns an error title (suitable for a 400) or null on success. On success,
    // `validated` holds the weeks parsed and sorted by start date.
    public static string? Validate(
        DateOnly seasonStart,
        DateOnly seasonEnd,
        IReadOnlyList<WeekInput> weeks,
        out List<ValidatedWeek> validated)
    {
        validated = [];

        foreach (var w in weeks)
        {
            if (!Enum.TryParse<WeekType>(w.WeekType, ignoreCase: false, out var type))
                return $"Invalid week type '{w.WeekType}'";

            if (w.StartDate > w.EndDate)
                return "Week start date is after its end date";

            if (w.StartDate < seasonStart || w.EndDate > seasonEnd)
                return "Week falls outside the season's date range";

            validated.Add(new ValidatedWeek(w.StartDate, w.EndDate, type));
        }

        validated.Sort((a, b) => a.StartDate.CompareTo(b.StartDate));

        for (var i = 1; i < validated.Count; i++)
        {
            if (validated[i].StartDate <= validated[i - 1].EndDate)
                return "Weeks must not overlap";
        }

        return null;
    }
}
