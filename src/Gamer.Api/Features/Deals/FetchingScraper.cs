using Gamer.Api.Domain;
using Gamer.Api.Features.Deals.Parsers;
using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals;

public record ScraperPages(
    Func<Store, string, string> SearchUrl,
    Func<Store, string> ListingUrl,
    IOfferParser SearchParser,
    IOfferParser ListingParser,
    string? WaitForSelector,
    Func<Store, int, string>? CatalogUrl = null);

public class FetchingScraper(string storeName, ScraperPages pages, IReadOnlyList<IPageFetcher> fetchers) : IStoreScraper
{
    private const int MaxCatalogPages = 15;

    public string StoreName => storeName;

    public async Task<ScrapeResult> SearchAsync(Store store, string title, CancellationToken cancellationToken)
    {
        ScrapeResult result = await RunAsync(pages.SearchUrl(store, title), pages.SearchParser, store, cancellationToken);
        if (!result.IsAvailable)
        {
            return result;
        }

        List<StoreOffer> matches = result.Offers.Where(offer => TitleMatcher.IsMatch(title, offer.Title)).ToList();
        return matches.Count == 0 ? ScrapeResult.Unavailable(StoreStatusReasons.NotFound) : ScrapeResult.Ok(matches);
    }

    public async Task<ScrapeResult> ListDealsAsync(Store store, CancellationToken cancellationToken)
    {
        ScrapeResult result = await RunAsync(pages.ListingUrl(store), pages.ListingParser, store, cancellationToken);
        if (!result.IsAvailable)
        {
            return result.FailureReason == StoreStatusReasons.NotFound
                ? ScrapeResult.Unavailable("The page returned no items; the store layout may have changed.")
                : result;
        }

        List<StoreOffer> deals = result.Offers.Where(offer => offer.DiscountPercent > 0).ToList();
        return deals.Count == 0 ? ScrapeResult.Unavailable("The page has no discounted games.") : ScrapeResult.Ok(deals);
    }

    public async Task<ScrapeResult> ListCatalogAsync(Store store, int maxItems, CancellationToken cancellationToken)
    {
        if (pages.CatalogUrl is null)
        {
            return ScrapeResult.Unavailable("This store has no browsable catalog.");
        }

        List<StoreOffer> offers = [];
        HashSet<string> seen = [];
        string failure = "The catalog has no results.";

        for (int page = 1; page <= MaxCatalogPages && offers.Count < maxItems; page++)
        {
            ScrapeResult result = await RunAsync(pages.CatalogUrl(store, page), pages.ListingParser, store, cancellationToken);
            if (!result.IsAvailable)
            {
                failure = result.FailureReason == StoreStatusReasons.NotFound ? failure : result.FailureReason ?? failure;
                break;
            }

            List<StoreOffer> fresh = result.Offers.Where(offer => seen.Add(offer.Url)).ToList();
            offers.AddRange(fresh);
            if (fresh.Count == 0)
            {
                break;
            }
        }

        return offers.Count == 0 ? ScrapeResult.Unavailable(failure) : ScrapeResult.Ok(offers.Take(maxItems).ToList());
    }

    private async Task<ScrapeResult> RunAsync(string url, IOfferParser parser, Store store, CancellationToken cancellationToken)
    {
        string reason = "No fetcher configured.";

        foreach (IPageFetcher fetcher in fetchers)
        {
            try
            {
                string content = await fetcher.FetchAsync(url, pages.WaitForSelector, cancellationToken);
                List<StoreOffer> offers = parser.Parse(content, store)
                    .Select(offer => offer with { Title = TitleCleaner.Clean(offer.Title) })
                    .Where(offer => offer.Title.Length > 0)
                    .ToList();

                return offers.Count == 0 ? ScrapeResult.Unavailable("The page has no results.") : ScrapeResult.Ok(offers);
            }
            catch (NoResultsException)
            {
                reason = StoreStatusReasons.NotFound;
            }
            catch (Exception exception) when (exception is ScrapeException or HttpRequestException
                || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                reason = exception.Message;
            }
        }

        return ScrapeResult.Unavailable(reason);
    }
}
