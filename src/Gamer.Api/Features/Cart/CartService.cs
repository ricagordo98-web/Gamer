using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Deals.Bundles;
using Gamer.Contracts.Deals;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Cart;

public interface ICartService
{
    Task<IReadOnlyList<CartItemDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<CartItemDto> SaveAsync(CartItemRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}

public class CartService(
    GamerDbContext db,
    CatalogCache catalog,
    BundleCache bundleCache,
    IEnumerable<IBundleSource> bundleSources,
    TimeProvider time) : ICartService
{
    private const decimal PriceTolerance = 0.005m;

    private static readonly TimeSpan EndingSoonWindow = TimeSpan.FromHours(48);

    public async Task<IReadOnlyList<CartItemDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        List<CartItem> items = (await db.CartItems.AsNoTracking().ToListAsync(cancellationToken)).OrderByDescending(i => i.AddedAt).ToList();
        List<string> gameUrls = items.Where(i => i.Kind == CartKinds.Game).Select(i => i.Url).ToList();
        Dictionary<string, Promotion> promotions = (await db.Promotions.AsNoTracking().Where(p => gameUrls.Contains(p.Url)).ToListAsync(cancellationToken))
            .GroupBy(p => p.Url)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CapturedAt).First());

        await RefreshBundlesAsync(items, cancellationToken);
        DateTimeOffset now = time.GetUtcNow();
        return items.Select(item => item.Kind == CartKinds.Bundle ? BundleDto(item, now) : GameDto(item, promotions, now)).ToList();
    }

    public async Task<CartItemDto> SaveAsync(CartItemRequest request, CancellationToken cancellationToken)
    {
        CartItem? item = await db.CartItems.FirstOrDefaultAsync(i => i.Kind == request.Kind && i.Url == request.Url, cancellationToken);
        if (item is null)
        {
            item = new CartItem
            {
                Kind = request.Kind,
                Store = request.Store,
                Title = request.Title,
                Url = request.Url,
                Currency = request.Currency,
                AddedAt = time.GetUtcNow(),
            };
            db.CartItems.Add(item);
        }

        item.Store = request.Store;
        item.Title = request.Title;
        item.ImageUrl = request.ImageUrl;
        item.Price = request.Price;
        item.OriginalPrice = request.OriginalPrice;
        item.Currency = request.Currency;
        item.EndsAt = request.EndsAt;
        item.Games = (request.Games ?? []).Select(g => new CartGame { Title = g.Title, ImageUrl = g.ImageUrl }).ToList();
        await db.SaveChangesAsync(cancellationToken);

        return (await GetAllAsync(cancellationToken)).Single(i => i.Id == item.Id);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        await db.CartItems.Where(i => i.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    private async Task RefreshBundlesAsync(List<CartItem> items, CancellationToken cancellationToken)
    {
        List<string> storeNames = items.Where(i => i.Kind == CartKinds.Bundle).Select(i => i.Store).Distinct().ToList();
        if (storeNames.Count == 0)
        {
            return;
        }

        List<Store> stores = await db.Stores.AsNoTracking().Where(s => s.IsEnabled && storeNames.Contains(s.Name)).ToListAsync(cancellationToken);
        foreach (Store store in stores)
        {
            if (bundleSources.FirstOrDefault(s => s.StoreName == store.Name) is { } source)
            {
                bundleCache.EnsureLoaded(store, source, false);
            }
        }
    }

    private CartItemDto GameDto(CartItem item, Dictionary<string, Promotion> promotions, DateTimeOffset now)
    {
        StoreOffer? catalogOffer = catalog.Get(item.Store).Offers.FirstOrDefault(o => o.Url == item.Url);
        (decimal? current, DateTimeOffset? endsAt) = promotions.TryGetValue(item.Url, out Promotion? promotion)
            ? (promotion.Price, promotion.EndsAt ?? item.EndsAt)
            : catalogOffer is not null ? (catalogOffer.Price, catalogOffer.EndsAt ?? item.EndsAt) : ((decimal?)null, item.EndsAt);

        string status = endsAt <= now ? CartStatuses.Ended
            : current > item.Price + PriceTolerance ? CartStatuses.PriceUp
            : current < item.Price - PriceTolerance ? CartStatuses.PriceDown
            : endsAt - now <= EndingSoonWindow ? CartStatuses.EndingSoon
            : CartStatuses.Active;

        return ToDto(item, current, endsAt, status);
    }

    private CartItemDto BundleDto(CartItem item, DateTimeOffset now)
    {
        BundleEntry entry = bundleCache.Get(item.Store);
        StoreBundle? live = entry.Bundles.FirstOrDefault(b => b.Url == item.Url);
        DateTimeOffset? endsAt = live?.EndsAt ?? item.EndsAt;
        bool gone = entry.UpdatedAt is not null && entry.UpdatedAt > item.AddedAt && live is null;

        string status = gone || endsAt <= now ? CartStatuses.Ended
            : endsAt - now <= EndingSoonWindow ? CartStatuses.EndingSoon
            : CartStatuses.Active;

        return ToDto(item, null, endsAt, status);
    }

    private static CartItemDto ToDto(CartItem item, decimal? current, DateTimeOffset? endsAt, string status) =>
        new(item.Id, item.Kind, item.Store, item.Title, item.Url, item.ImageUrl, item.Price, item.OriginalPrice, item.Currency, current, endsAt,
            item.AddedAt, status, item.Games.Select(g => new CartGameDto(g.Title, g.ImageUrl)).ToList());
}
