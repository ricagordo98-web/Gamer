namespace Gamer.Api.Features.Deals;

public interface IPageFetcher
{
    Task<string> FetchAsync(string url, string? waitForSelector, CancellationToken cancellationToken);
}
