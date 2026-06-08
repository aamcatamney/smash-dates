namespace smash_dates.Services.Notifications;

// Shared rendering for outbound notification emails. Bodies are authored per message type at
// enqueue time; the version footer is a delivery concern applied uniformly here so every sender
// stamps the same trailer and no message type can forget it.
public static class NotificationEmail
{
    // Appends the build version as a footer, separated by the standard "--" signature delimiter,
    // so every delivered email is traceable to the build that sent it.
    public static string BodyWithVersionFooter(string body, string version) =>
        $"{body}\n\n--\nsmash-dates {version}";
}
