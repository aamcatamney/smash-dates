using smash_dates.Services;

namespace smash_dates.Endpoints.Ops;

// Liveness + build-version endpoints for ops / deploy verification. Both anonymous.
public static class OpsEndpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/api/version", (IAppVersion version) => Results.Ok(new { version = version.Current }));
        return app;
    }
}
