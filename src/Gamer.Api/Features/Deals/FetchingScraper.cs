using Gamer.Api.Domain;
using Gamer.Api.Features.Deals.Parsers;

namespace Gamer.Api.Features.Deals;

public class FetchingScraper(
    string storeName,
    Func<Store, string, string> buildUrl,
    IOfferParser parser,
    IReadOnlyList<IPageFetcher> fetchers) : IStoreScraper
{
    public string StoreName => storeName;

    public async Task<ScrapeResult> SearchAsync(Store store, string title, CancellationToken cancellationToken)
    {
        string url = buildUrl(store, title);
        string reason = "No fetcher configured.";

        foreach (IPageFetcher fetcher in fetchers)
        {
            try
            {
                string content = await fetcher.FetchAsync(url, cancellationToken);
                List<StoreOffer> matches = parser.Parse(content, store)
                    .Where(offer => TitleMatcher.IsMatch(title, offer.Title))
                    .ToList();

                return matches.Count == 0
                    ? ScrapeResult.Unavailable("No matching result.")
                    : ScrapeResult.Ok(matches);
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
