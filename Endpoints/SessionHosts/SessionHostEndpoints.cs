namespace smash_dates.Endpoints.SessionHosts;

public static class SessionHostEndpoints
{
    public static IEndpointRouteBuilder MapSessionHostEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/session-hosts").RequireAuthorization();
        group.MapGrantSessionHostEndpoint();
        group.MapRevokeSessionHostEndpoint();
        group.MapListSessionHostsEndpoint();
        return app;
    }
}
