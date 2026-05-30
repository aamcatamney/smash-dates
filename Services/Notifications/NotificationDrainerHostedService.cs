using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace smash_dates.Services.Notifications;

// Periodically drains the notification outbox. NotificationDrainer is scoped (it uses
// scoped repositories), so a fresh scope is created each tick.
public sealed class NotificationDrainerHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDrainerHostedService> _logger;

    public NotificationDrainerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDrainerHostedService> logger)
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
                var drainer = scope.ServiceProvider.GetRequiredService<NotificationDrainer>();
                await drainer.DrainAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification drain failed; will retry next interval.");
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
