using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals;

public enum RefreshScope
{
    All,
    Promotions,
    Prices,
}

public class RefreshCoordinator(IServiceScopeFactory scopes, TimeProvider time, ILogger<RefreshCoordinator> logger)
{
    private int _running;
    private DateTimeOffset? _lastCompletedAt;
    private DateTimeOffset? _promotionsCompletedAt;

    public RefreshStatusDto Status => new(_running == 1, _lastCompletedAt, _promotionsCompletedAt);

    public bool TryStart(RefreshScope scope = RefreshScope.All)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }

        _ = Task.Run(() => RunAsync(scope));
        return true;
    }

    private async Task RunAsync(RefreshScope scope)
    {
        try
        {
            await using AsyncServiceScope serviceScope = scopes.CreateAsyncScope();

            if (scope is RefreshScope.All or RefreshScope.Promotions)
            {
                await RunStepAsync("Promotions", () => serviceScope.ServiceProvider.GetRequiredService<IPromotionService>().RefreshAsync(CancellationToken.None));
                _promotionsCompletedAt = time.GetUtcNow();
            }

            if (scope is RefreshScope.All or RefreshScope.Prices)
            {
                await RunStepAsync("Prices", () => serviceScope.ServiceProvider.GetRequiredService<IDealService>().RefreshAsync(CancellationToken.None));
                _lastCompletedAt = time.GetUtcNow();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private async Task RunStepAsync(string name, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{Step} refresh failed.", name);
        }
    }
}
