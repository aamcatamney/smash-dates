using System.Reflection;

namespace smash_dates.Services;

// The running build's version: the CalVer tag stamped into the container image as APP_VERSION,
// else the assembly informational version, else "dev" for a plain local run. Single source of
// truth for both the GET /api/version endpoint and the notification email footer, so the two
// can never drift. Resolved once at startup (registered as a singleton).
public interface IAppVersion
{
    string Current { get; }
}

public sealed class AppVersion : IAppVersion
{
    public string Current { get; } =
        Environment.GetEnvironmentVariable("APP_VERSION") is { Length: > 0 } env
            ? env
            : Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
              ?? "dev";
}
