using smash_dates.Services.Notifications;

namespace smash_dates.UnitTests.Services.Notifications;

public sealed class NotificationEmailTests
{
    [Fact]
    public void BodyWithVersionFooter_AppendsVersionAfterASignatureDelimiter()
    {
        var result = NotificationEmail.BodyWithVersionFooter("Your match is confirmed.", "v2026.6.0");

        result.Should().Be("Your match is confirmed.\n\n--\nsmash-dates v2026.6.0");
    }

    [Fact]
    public void BodyWithVersionFooter_KeepsTheOriginalBodyIntact()
    {
        const string body = "Line one.\nLine two.";

        var result = NotificationEmail.BodyWithVersionFooter(body, "dev");

        result.Should().StartWith(body);
        result.Should().EndWith("smash-dates dev");
    }
}
