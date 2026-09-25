namespace Kinsat.Api.Services;

public sealed class LtvMonitorService(ILogger<LtvMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("LTV monitor started. No active loans to watch yet, this is a placeholder loop.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // TODO: once ICustodyProvider and the loan lifecycle exist, resolve active
            // loans per tenant, recompute LTV against a price oracle, and publish
            // LtvBreached through IEventBus on threshold breaches (see Kinsat.Sdk.Events).
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
