namespace smash_dates.Services.Notifications;

// Bound from the "Smtp" configuration section. When Host is empty the app falls back to the
// logging sender, so SMTP is purely opt-in (a config swap) and dev/tests need no mail server.
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@smash-dates.local";
    public string FromName { get; set; } = "Smash Dates";

    // STARTTLS is negotiated when the server advertises it; set false only for a plaintext relay.
    public bool UseStartTls { get; set; } = true;
}
