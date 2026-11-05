using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Deals;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Deals;

public interface IPromotionService
{
    Task RefreshAsync(CancellationToken cancellationToken);
    Task<PromotionPageDto> QueryAsync(PromotionQuery query, CancellationToken cancellationToken);
}

public class PromotionService(GamerDbContext db, IEnumerable<IStoreScraper> scrapers, PromotionStatusBoard board, TimeProvider time) : IPromotionService
{
    private const int MaxPageSize = 120;

    private readonly Dictionary<string, IStoreScraper> _scrapers = scrapers.ToDictionary(s => s.StoreName);

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        List<Store> stores = await db.Stores.AsNoTracking().Where(s => s.IsEnabled).OrderBy(s => s.Id).ToListAsync(cancellationToken);
        (Store Store, ScrapeResult Result)[] results = await Task.WhenAll(stores.Select(async store =>
            (store, await ScrapeAsync(store, cancellationToken))));

        foreach ((Store store, ScrapeResult result) in results)
        {
            board.Set(store.Name, result.FailureReason);
            if (result.IsAvailable)
            {
                await ReplaceAsync(store, result.Offers, cancellationToken);
            }
        }
    }

    public async Task<PromotionPageDto> QueryAsync(PromotionQuery query, CancellationToken cancellationToken)
    {
        List<Promotion> promotions = await db.Promotions.AsNoTracking().Include(p => p.Store).ToListAsync(cancellationToken);
        HashSet<string> library = (await db.SteamLibrary.AsNoTracking().Select(i => i.Title).ToListAsync(cancellationToken))
            .Select(TitleMatcher.Normalize).ToHashSet();
        HashSet<string> wishlist = (await db.Wishlist.AsNoTracking().Select(i => i.Title).ToListAsync(cancellationToken))
            .Select(TitleMatcher.Normalize).ToHashSet();
        List<Store> stores = await db.Stores.AsNoTracking().Where(s => s.IsEnabled).OrderBy(s => s.Id).ToListAsync(cancellationToken);
        HashSet<string> storeFilter = (query.Store ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IEnumerable<PromotionDto> filtered = promotions
            .GroupBy(p => p.TitleKey)
            .Select(group => BuildGroup(group.ToList(), storeFilter, library, wishlist))
            .OfType<PromotionDto>()
            .Where(item => Matches(item, query));

        List<PromotionDto> ordered = Sort(filtered, query.Sort).ToList();
        int take = Math.Clamp(query.Take, 1, MaxPageSize);
        List<PromotionDto> page = ordered.Skip(Math.Max(query.Skip, 0)).Take(take).ToList();

        return new PromotionPageDto(page, ordered.Count, StoreSummaries(stores, promotions), promotions.Count == 0 ? null : promotions.Max(p => p.CapturedAt));
    }

    private async Task<ScrapeResult> ScrapeAsync(Store store, CancellationToken cancellationToken) =>
        _scrapers.TryGetValue(store.Name, out IStoreScraper? scraper)
            ? await scraper.ListDealsAsync(store, cancellationToken)
            : ScrapeResult.Unavailable("No scraper for this store.");

    private async Task ReplaceAsync(Store store, IReadOnlyList<StoreOffer> offers, CancellationToken cancellationToken)
    {
        DateTimeOffset now = time.GetUtcNow();
        List<Promotion> fresh = offers
            .GroupBy(o => o.Url)
            .Select(group => group.First())
            .Select(o => new Promotion
            {
                StoreId = store.Id,
                Title = o.Title,
                TitleKey = TitleMatcher.Normalize(o.Title),
                ImageUrl = o.ImageUrl,
                Price = o.Price,
                OriginalPrice = o.OriginalPrice,
                DiscountPercent = o.DiscountPercent,
                Url = o.Url,
                Currency = o.Currency,
                EndsAt = o.EndsAt,
                CapturedAt = now,
            })
            .Where(p => p.TitleKey.Length > 0)
            .ToList();

        await db.Promotions.Where(p => p.StoreId == store.Id).ExecuteDeleteAsync(cancellationToken);
        db.Promotions.AddRange(fresh);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    private static PromotionDto? BuildGroup(List<Promotion> group, HashSet<string> storeFilter, HashSet<string> library, HashSet<string> wishlist)
    {
        string? currency = group
            .GroupBy(p => p.Currency)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key)
            .FirstOrDefault();

        List<Promotion> cheapestPerStore = group
            .Where(p => p.Currency == currency)
            .GroupBy(p => p.StoreId)
            .Select(g => g.OrderBy(p => p.Price).ThenByDescending(p => p.DiscountPercent).First())
            .OrderBy(p => p.Price)
            .ThenByDescending(p => p.DiscountPercent)
            .ToList();

        Promotion? best = storeFilter.Count == 0
            ? cheapestPerStore.FirstOrDefault()
            : cheapestPerStore.FirstOrDefault(p => storeFilter.Contains(p.Store.Name));
        if (best is null)
        {
            return null;
        }

        return new PromotionDto(
            best.Title,
            best.ImageUrl ?? cheapestPerStore.Select(p => p.ImageUrl).FirstOrDefault(i => i is not null),
            ToOffer(best),
            best.OriginalPrice - best.Price,
            cheapestPerStore.Where(p => p != best).Select(ToOffer).ToList(),
            library.Contains(best.TitleKey),
            wishlist.Contains(best.TitleKey),
            ExtraContent.IsExtra(best.Title));
    }

    private static OfferDto ToOffer(Promotion p) =>
        new(p.Store.Name, p.Title, p.Price, p.OriginalPrice, p.DiscountPercent, p.Url, p.Currency, p.ImageUrl, p.EndsAt);

    private static bool Matches(PromotionDto item, PromotionQuery query)
    {
        if (query.MinDiscount is > 0 && item.Best.DiscountPercent < query.MinDiscount)
        {
            return false;
        }

        if (query.MaxPrice is not null && item.Best.Price > query.MaxPrice)
        {
            return false;
        }

        if ((query.OnlyWishlist && !item.OnWishlist) || (query.HideOwned && item.Owned)
            || (query.HideExtras && item.IsExtra) || (query.MultiStore && item.Others.Count == 0))
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

    private static IEnumerable<PromotionDto> Sort(IEnumerable<PromotionDto> items, string? sort) => sort switch
    {
        PromotionSort.Saving => items.OrderByDescending(i => i.Saving).ThenByDescending(i => i.Best.DiscountPercent),
        PromotionSort.Price => items.OrderBy(i => i.Best.Price).ThenByDescending(i => i.Best.DiscountPercent),
        PromotionSort.Name => items.OrderBy(i => i.Title, StringComparer.OrdinalIgnoreCase),
        _ => items.OrderByDescending(i => i.Best.DiscountPercent).ThenByDescending(i => i.Saving),
    };

    private List<PromotionStoreDto> StoreSummaries(List<Store> stores, List<Promotion> promotions) =>
        stores.Select(store =>
        {
            List<Promotion> rows = promotions.Where(p => p.StoreId == store.Id).ToList();
            return new PromotionStoreDto(store.Name, rows.Count, rows.Count == 0 ? null : rows.Max(p => p.CapturedAt), board.ErrorFor(store.Name));
        }).ToList();
}
