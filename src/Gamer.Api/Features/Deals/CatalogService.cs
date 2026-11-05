using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Deals;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Deals;

public interface ICatalogService
{
    Task<CatalogPageDto?> QueryAsync(CatalogQuery query, bool refresh, CancellationToken cancellationToken);
}

public class CatalogService(GamerDbContext db, IEnumerable<IStoreScraper> scrapers, CatalogCache cache) : ICatalogService
{
    private const int MaxPageSize = 120;

    private readonly Dictionary<string, IStoreScraper> _scrapers = scrapers.ToDictionary(s => s.StoreName);

    public async Task<CatalogPageDto?> QueryAsync(CatalogQuery query, bool refresh, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Store))
        {
            return null;
        }

        Store? store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.IsEnabled && s.Name == query.Store, cancellationToken);
        if (store is null || !_scrapers.TryGetValue(store.Name, out IStoreScraper? scraper))
        {
            return null;
        }

        CatalogEntry entry = cache.EnsureLoaded(store, scraper, refresh);
        HashSet<string> library = (await db.SteamLibrary.AsNoTracking().Select(i => i.Title).ToListAsync(cancellationToken))
            .Select(TitleMatcher.Normalize).ToHashSet();
        HashSet<string> wishlist = (await db.Wishlist.AsNoTracking().Select(i => i.Title).ToListAsync(cancellationToken))
            .Select(TitleMatcher.Normalize).ToHashSet();

        List<PromotionDto> all = entry.Offers
            .Select((offer, index) => (offer, index))
            .OrderByDescending(x => x.offer.DiscountPercent > 0)
            .ThenByDescending(x => x.offer.DiscountPercent)
            .ThenBy(x => x.index)
            .Select(x => ToItem(x.offer, library, wishlist))
            .ToList();

        List<PromotionDto> filtered = all.Where(item => Matches(item, query)).ToList();
        int take = Math.Clamp(query.Take, 1, MaxPageSize);
        List<PromotionDto> page = filtered.Skip(Math.Max(query.Skip, 0)).Take(take).ToList();

        return new CatalogPageDto(page, filtered.Count, all.Count(i => i.Best.DiscountPercent > 0), entry.Loading, entry.UpdatedAt, entry.Error);
    }

    private static PromotionDto ToItem(StoreOffer offer, HashSet<string> library, HashSet<string> wishlist)
    {
        string key = TitleMatcher.Normalize(offer.Title);
        OfferDto dto = new(offer.Store, offer.Title, offer.Price, offer.OriginalPrice, offer.DiscountPercent, offer.Url, offer.Currency, offer.ImageUrl, offer.EndsAt);
        return new PromotionDto(offer.Title, offer.ImageUrl, dto, offer.OriginalPrice - offer.Price, [], library.Contains(key), wishlist.Contains(key), ExtraContent.IsExtra(offer.Title));
    }

    private static bool Matches(PromotionDto item, CatalogQuery query)
    {
        if ((query.OnlySale && item.Best.DiscountPercent == 0) || (query.HideOwned && item.Owned) || (query.HideExtras && item.IsExtra))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return true;
        }

        string title = TitleMatcher.Normalize(item.Title);
        return TitleMatcher.Normalize(query.Search).Split(' ', StringSplitOptions.RemoveEmptyEntries).All(title.Contains);
    }
}
