using System.Reflection;

namespace smash_dates.Endpoints.Ops;

// Liveness + build-version endpoints for ops / deploy verification. Both anonymous.
public static class OpsEndpoints
{
    public static IEndpointRouteBuilder MapOpsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/api/version", () => Results.Ok(new { version = ResolveVersion() }));
        return app;
    }

    // APP_VERSION is stamped into the container image from the CalVer release tag; falls back
    // to the assembly informational version, then "dev" for a plain local run.
    private static string ResolveVersion() =>
        Environment.GetEnvironmentVariable("APP_VERSION") is { Length: > 0 } env
            ? env
            : Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
              ?? "dev";
}
