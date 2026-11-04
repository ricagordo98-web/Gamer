using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals;

public interface IStoreScraper
{
    string StoreName { get; }

    Task<ScrapeResult> SearchAsync(Store store, string title, CancellationToken cancellationToken);

    Task<ScrapeResult> ListDealsAsync(Store store, CancellationToken cancellationToken);

    Task<ScrapeResult> ListCatalogAsync(Store store, int maxItems, CancellationToken cancellationToken);
}
