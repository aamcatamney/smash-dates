using smash_dates.Repositories;

namespace smash_dates.Endpoints.Clubs;

public static class GetClubEndpoint
{
    public sealed record ClubDetail(Guid Id, string Name, string ShortCode, string ContactEmail, string? Notes);

    public static IEndpointRouteBuilder MapGetClubEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{id:guid}", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid id, IClubRepository clubs, CancellationToken ct)
    {
        var club = await clubs.GetByIdAsync(id, ct);
        return club is null
            ? Results.NotFound()
            : Results.Ok(new ClubDetail(club.Id, club.Name, club.ShortCode, club.ContactEmail, club.Notes));
    }
}
