using Gamer.Api.Features.Steam;

namespace Gamer.Api.Features.Deals;

public class PriceRefreshService(IServiceScopeFactory scopes, RefreshCoordinator coordinator, ILogger<PriceRefreshService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            int hours = SteamSettingsService.DefaultRefreshHours;
            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                hours = await scope.ServiceProvider.GetRequiredService<ISteamSettingsService>().GetRefreshHoursAsync(stoppingToken);
                if (hours > 0)
                {
                    coordinator.TryStart();
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Could not schedule the price refresh.");
            }

            await Task.Delay(TimeSpan.FromHours(hours > 0 ? hours : 1), stoppingToken);
        }
    }
}
