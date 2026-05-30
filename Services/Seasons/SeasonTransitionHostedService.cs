using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace smash_dates.Services.Seasons;

// Periodically advances season lifecycle by the calendar. SeasonTransitioner is scoped
// (scoped repositories), so a fresh scope is created each tick.
public sealed class SeasonTransitionHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SeasonTransitionHostedService> _logger;

    public SeasonTransitionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SeasonTransitionHostedService> logger)
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
                var transitioner = scope.ServiceProvider.GetRequiredService<SeasonTransitioner>();
                await transitioner.RunAsync(DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Season transition run failed; will retry next interval.");
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
