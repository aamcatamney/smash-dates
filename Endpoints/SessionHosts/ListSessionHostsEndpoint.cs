using smash_dates.Repositories;

namespace smash_dates.Endpoints.SessionHosts;

public static class ListSessionHostsEndpoint
{
    public sealed record HostDto(Guid UserId, System.DateTime GrantedAt);

    public static IEndpointRouteBuilder MapListSessionHostsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle);
        return app;
    }

    private static async Task<IResult> Handle(Guid clubId, ISessionHostRepository hosts, CancellationToken ct)
    {
        var rows = await hosts.ListByClubAsync(clubId, ct);
        return Results.Ok(rows.Select(h => new HostDto(h.UserId, h.GrantedAt)));
    }
}
