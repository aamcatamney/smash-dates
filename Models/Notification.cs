namespace smash_dates.Models;

// An outbox entry: a message queued for delivery to a recipient email. SentAt is null
// until a sender delivers it (delivery itself is deferred).
public sealed class Notification
{
    public Guid Id { get; init; }
    public string RecipientEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? SentAt { get; init; }
}
