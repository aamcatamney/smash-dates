using smash_dates.Services.Scheduling;

namespace smash_dates.UnitTests.Services.Scheduling;

// The number of Matches a Venue can host simultaneously in one (Venue, Date) slot:
// min(maxConcurrentMatches, floor(courts / courtsPerMatch)).
public class VenueSlotCapacityTests
{
    [Theory]
    [InlineData(4, 2, 2, 2)] // 4 courts / 2-per-match = 2, under the cap
    [InlineData(2, 2, 2, 1)] // only enough courts for one match
    [InlineData(3, 2, 2, 1)] // odd court left over
    [InlineData(6, 2, 2, 2)] // courts allow 3 but the maxConcurrent cap bites
    [InlineData(1, 2, 2, 0)] // not enough courts for any match
    [InlineData(6, 1, 2, 1)] // maxConcurrent of 1 caps it
    [InlineData(6, 2, 3, 2)] // 6 / 3-per-match = 2
    [InlineData(4, 2, 3, 1)] // 4 / 3-per-match = 1
    public void Compute_TakesTheMinOfCapAndCourtsDivision(
        int courts, int maxConcurrent, int courtsPerMatch, int expected)
        => VenueSlotCapacity.Compute(courts, maxConcurrent, courtsPerMatch).Should().Be(expected);

    [Fact]
    public void Compute_GuardsAgainstZeroCourtsPerMatch()
        => VenueSlotCapacity.Compute(4, 2, 0).Should().Be(0);
}
