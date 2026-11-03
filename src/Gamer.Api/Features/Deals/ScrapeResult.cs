namespace Gamer.Api.Features.Deals;

public record ScrapeResult(IReadOnlyList<StoreOffer> Offers, string? FailureReason)
{
    public bool IsAvailable => FailureReason is null;

    public static ScrapeResult Ok(IReadOnlyList<StoreOffer> offers) => new(offers, null);

    public static ScrapeResult Unavailable(string reason) => new([], reason);
}
