using smash_dates.Repositories;
using smash_dates.Services.Auth;

namespace smash_dates.Endpoints.Notifications;

public static class NotificationEndpoints
{
    public sealed record NotificationSummary(
        Guid Id, string RecipientEmail, string Subject, string Body, DateTime CreatedAt, DateTime? SentAt);

    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization();

        // The outbox is an operational/debug view; SystemAdmin only.
        group.MapGet("/", Handle).RequireAuthorization(AuthorizationPolicies.SystemAdmin);
        return app;
    }

    private static async Task<IResult> Handle(INotificationRepository notifications, CancellationToken ct)
    {
        var rows = await notifications.ListRecentAsync(ct: ct);
        return Results.Ok(rows
            .Select(n => new NotificationSummary(n.Id, n.RecipientEmail, n.Subject, n.Body, n.CreatedAt, n.SentAt))
            .ToArray());
    }
}
