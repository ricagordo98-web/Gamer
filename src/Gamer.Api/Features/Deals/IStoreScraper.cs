using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals;

public interface IStoreScraper
{
    string StoreName { get; }

    Task<ScrapeResult> SearchAsync(Store store, string title, CancellationToken cancellationToken);
}
