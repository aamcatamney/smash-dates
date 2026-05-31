using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace smash_dates.IntegrationTests.Infrastructure;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", _connectionString);
        builder.UseSetting("RateLimit:Auth:PermitLimit", "10000");
        builder.UseEnvironment("Development");

        // Tests drive the background workers explicitly (e.g. ScheduleRunner), so the periodic
        // hosted services are removed for determinism — no ticks racing the test's own runs.
        builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
    }
}
