using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals;

public class RefreshCoordinator(IServiceScopeFactory scopes, TimeProvider time, ILogger<RefreshCoordinator> logger)
{
    private int _running;
    private DateTimeOffset? _lastCompletedAt;

    public RefreshStatusDto Status => new(_running == 1, _lastCompletedAt);

    public bool TryStart()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }

        _ = Task.Run(RunAsync);
        return true;
    }

    private async Task RunAsync()
    {
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IDealService>().RefreshAsync(CancellationToken.None);
            _lastCompletedAt = time.GetUtcNow();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Price refresh failed.");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}
