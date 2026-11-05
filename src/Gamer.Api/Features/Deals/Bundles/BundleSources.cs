using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Bundles;

public class HumbleBundleSource(IPageFetcher fetcher) : IBundleSource
{
    public string StoreName => ScraperCatalog.HumbleBundle;

    public async Task<IReadOnlyList<StoreBundle>> ListAsync(Store store, CancellationToken cancellationToken)
    {
        string listingHtml = await fetcher.FetchAsync($"{store.BaseUrl}games", null, cancellationToken);
        List<StoreBundle> bundles = [];

        foreach (HumbleListing listing in HumbleBundleParser.ParseListing(listingHtml, store.BaseUrl))
        {
            try
            {
                string html = await fetcher.FetchAsync(listing.Url, null, cancellationToken);
                if (HumbleBundleParser.ParseBundle(html, listing) is { } bundle)
                {
                    bundles.Add(bundle);
                }
            }
            catch (Exception exception) when (exception is ScrapeException or HttpRequestException or System.Text.Json.JsonException or KeyNotFoundException)
            {
            }
        }

        return bundles;
    }
}

public class FanaticalBundleSource(IPageFetcher fetcher) : IBundleSource
{
    public string StoreName => ScraperCatalog.Fanatical;

    public async Task<IReadOnlyList<StoreBundle>> ListAsync(Store store, CancellationToken cancellationToken)
    {
        string json = await fetcher.FetchAsync($"{store.BaseUrl}api/all/en", null, cancellationToken);
        return FanaticalBundleParser.Parse(json, store.BaseUrl, store.Currency);
    }
}
