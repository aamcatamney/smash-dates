using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using smash_dates.Repositories;

namespace smash_dates.Services.Auth;

// Periodically prunes spent auth tokens (used or expired) so the table doesn't grow without
// bound. IAuthTokenRepository is scoped, so a fresh scope is created each tick.
public sealed class AuthTokenCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuthTokenCleanupHostedService> _logger;

    public AuthTokenCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuthTokenCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tokens = scope.ServiceProvider.GetRequiredService<IAuthTokenRepository>();
                var removed = await tokens.DeleteSpentAsync(stoppingToken);
                if (removed > 0)
                {
                    _logger.LogInformation("Pruned {Count} spent auth token(s).", removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auth token cleanup failed; will retry next interval.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
