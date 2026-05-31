namespace smash_dates.Endpoints.Pegboard;

public static class PegboardEndpoints
{
    public static IEndpointRouteBuilder MapPegboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clubs/{clubId:guid}/pegboard/sessions").RequireAuthorization();

        // Reads (any authenticated user)
        group.MapListSessionsEndpoint();
        group.MapGetSessionEndpoint();
        group.MapGetBoardEndpoint();
        group.MapStreamBoardEndpoint();

        // Lifecycle (host/admin)
        group.MapOpenSessionEndpoint();
        group.MapCloseSessionEndpoint();

        // Courts & attendances (host/admin)
        group.MapAddCourtEndpoint();
        group.MapRemoveCourtEndpoint();
        group.MapAddAttendanceEndpoint();
        group.MapSetAttendanceStatusEndpoint();
        group.MapRemoveAttendanceEndpoint();
        group.MapSuggestFillEndpoint();
        group.MapStartGameEndpoint();
        group.MapFinishGameEndpoint();
        group.MapCancelGameEndpoint();
        return app;
    }
}
