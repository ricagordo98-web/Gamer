using System.Collections.Concurrent;
using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals;

public record CatalogEntry(IReadOnlyList<StoreOffer> Offers, DateTimeOffset? UpdatedAt, bool Loading, string? Error)
{
    public static CatalogEntry Empty { get; } = new([], null, false, null);
}

public class CatalogCache(TimeProvider time)
{
    public const int MaxItems = 1000;

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<string, CatalogEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public CatalogEntry Get(string storeName) => _entries.GetValueOrDefault(storeName) ?? CatalogEntry.Empty;

    public CatalogEntry EnsureLoaded(Store store, IStoreScraper scraper, bool force)
    {
        lock (_gate)
        {
            CatalogEntry current = Get(store.Name);
            bool stale = current.UpdatedAt is null || time.GetUtcNow() - current.UpdatedAt > Lifetime;
            if (current.Loading || !(force || stale || (current.Error is not null && current.Offers.Count == 0)))
            {
                return current;
            }

            CatalogEntry loading = current with { Loading = true, Error = null };
            _entries[store.Name] = loading;
            _ = Task.Run(() => LoadAsync(store, scraper));
            return loading;
        }
    }

    private async Task LoadAsync(Store store, IStoreScraper scraper)
    {
        CatalogEntry previous = Get(store.Name);
        try
        {
            Task<ScrapeResult> deals = scraper.ListDealsAsync(store, CancellationToken.None);
            Task<ScrapeResult> catalog = scraper.ListCatalogAsync(store, MaxItems, partial => PublishPartial(store.Name, previous, deals, partial), CancellationToken.None);
            await Task.WhenAll(deals, catalog);

            List<StoreOffer> merged = Merge(deals.Result, catalog.Result);
            _entries[store.Name] = merged.Count > 0
                ? new CatalogEntry(merged, time.GetUtcNow(), false, null)
                : previous with { Loading = false, Error = catalog.Result.FailureReason ?? deals.Result.FailureReason };
        }
        catch (Exception exception)
        {
            _entries[store.Name] = previous with { Loading = false, Error = exception.Message };
        }
    }

    private void PublishPartial(string storeName, CatalogEntry previous, Task<ScrapeResult> deals, IReadOnlyList<StoreOffer> partial)
    {
        ScrapeResult dealsSoFar = deals.IsCompletedSuccessfully ? deals.Result : ScrapeResult.Unavailable(string.Empty);
        List<StoreOffer> merged = Merge(dealsSoFar, ScrapeResult.Ok(partial));
        if (merged.Count > previous.Offers.Count)
        {
            _entries[storeName] = new CatalogEntry(merged, time.GetUtcNow(), true, null);
        }
    }

    private static List<StoreOffer> Merge(ScrapeResult deals, ScrapeResult catalog) =>
        (deals.IsAvailable ? deals.Offers : [])
            .Concat(catalog.IsAvailable ? catalog.Offers : [])
            .GroupBy(offer => offer.Url)
            .Select(group => group.OrderByDescending(offer => offer.DiscountPercent).First())
            .Take(MaxItems)
            .ToList();
}
