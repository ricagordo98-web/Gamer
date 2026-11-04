using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals;

public static class DealBuilder
{
    public static DealDto Build(
        string title,
        int? trackedId,
        long? steamAppId,
        bool owned,
        bool onWishlist,
        IReadOnlyList<StoreOffer> offers,
        IReadOnlyList<StoreStatusDto> stores)
    {
        string? currency = offers
            .GroupBy(o => o.Currency)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault();

        List<OfferDto> ranked = offers
            .Where(o => o.Currency == currency)
            .OrderBy(o => o.Price)
            .ThenByDescending(o => o.DiscountPercent)
            .Select(o => new OfferDto(o.Store, o.Title, o.Price, o.OriginalPrice, o.DiscountPercent, o.Url, o.Currency, o.ImageUrl))
            .ToList();

        OfferDto? best = ranked.FirstOrDefault();
        decimal? saving = ranked.Count > 1 ? ranked[1].Price - ranked[0].Price : null;

        return new DealDto(title, trackedId, steamAppId, owned, onWishlist, best, saving, ranked, stores);
    }
}
