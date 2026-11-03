using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals;

public record StoreOutcome(Store Store, StoreOffer? Offer, string? FailureReason);

public class StoreSearcher(IEnumerable<IStoreScraper> scrapers)
{
    private readonly Dictionary<string, IStoreScraper> _scrapers = scrapers.ToDictionary(s => s.StoreName);

    public async Task<IReadOnlyList<StoreOutcome>> SearchAsync(IReadOnlyList<Store> stores, string title, CancellationToken cancellationToken)
    {
        StoreOutcome[] outcomes = await Task.WhenAll(stores.Select(store => SearchStoreAsync(store, title, cancellationToken)));
        return outcomes;
    }

    private async Task<StoreOutcome> SearchStoreAsync(Store store, string title, CancellationToken cancellationToken)
    {
        if (!_scrapers.TryGetValue(store.Name, out IStoreScraper? scraper))
        {
            return new StoreOutcome(store, null, "No scraper for this store.");
        }

        ScrapeResult result = await scraper.SearchAsync(store, title, cancellationToken);
        if (!result.IsAvailable)
        {
            return new StoreOutcome(store, null, result.FailureReason);
        }

        StoreOffer cheapest = result.Offers.OrderBy(o => o.Price).First();
        return new StoreOutcome(store, cheapest, null);
    }
}
