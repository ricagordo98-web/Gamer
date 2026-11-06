using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals.Bundles;
using Gamer.Contracts.Deals;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Deals;

public enum RefreshScope
{
    All,
    Promotions,
    Prices,
    Stores,
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

            Task catalogs = scope is RefreshScope.All or RefreshScope.Stores
                ? RunStepAsync("Catalogs", () => WarmCatalogsAsync())
                : Task.CompletedTask;

            if (scope is RefreshScope.All or RefreshScope.Promotions or RefreshScope.Stores)
            {
                await RunStepAsync("Promotions", () => serviceScope.ServiceProvider.GetRequiredService<IPromotionService>().RefreshAsync(CancellationToken.None));
                _promotionsCompletedAt = time.GetUtcNow();
            }

            if (scope is RefreshScope.All or RefreshScope.Prices)
            {
                await RunStepAsync("Prices", () => serviceScope.ServiceProvider.GetRequiredService<IDealService>().RefreshAsync(CancellationToken.None));
                _lastCompletedAt = time.GetUtcNow();
            }

            await catalogs;
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private static readonly TimeSpan CatalogWait = TimeSpan.FromMinutes(10);

    // Starts every enabled store's game catalog and bundle list (they load in the background) and waits for them to settle.
    private async Task WarmCatalogsAsync()
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        GamerDbContext db = services.GetRequiredService<GamerDbContext>();
        CatalogCache catalogs = services.GetRequiredService<CatalogCache>();
        BundleCache bundles = services.GetRequiredService<BundleCache>();
        Dictionary<string, IStoreScraper> scrapers = services.GetRequiredService<IEnumerable<IStoreScraper>>().ToDictionary(s => s.StoreName);
        Dictionary<string, IBundleSource> sources = services.GetRequiredService<IEnumerable<IBundleSource>>().ToDictionary(s => s.StoreName);
        List<Store> stores = await db.Stores.AsNoTracking().Where(s => s.IsEnabled).ToListAsync();

        foreach (Store store in stores)
        {
            if (scrapers.TryGetValue(store.Name, out IStoreScraper? scraper))
            {
                catalogs.EnsureLoaded(store, scraper, true);
            }

            if (sources.TryGetValue(store.Name, out IBundleSource? source))
            {
                bundles.EnsureLoaded(store, source, true);
            }
        }

        DateTimeOffset deadline = time.GetUtcNow() + CatalogWait;
        while (time.GetUtcNow() < deadline && stores.Any(s => catalogs.Get(s.Name).Loading || bundles.Get(s.Name).Loading))
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
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
