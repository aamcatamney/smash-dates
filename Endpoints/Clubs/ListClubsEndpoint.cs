using smash_dates.Repositories;

namespace smash_dates.Endpoints.Clubs;

public static class ListClubsEndpoint
{
    public sealed record ClubSummary(Guid Id, string Name, string ShortCode, string ContactEmail, string? Notes);

    public static IEndpointRouteBuilder MapListClubsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(IClubRepository clubs, CancellationToken ct)
    {
        var rows = await clubs.ListAsync(ct);
        return Results.Ok(rows.Select(c => new ClubSummary(c.Id, c.Name, c.ShortCode, c.ContactEmail, c.Notes)).ToArray());
    }
}
