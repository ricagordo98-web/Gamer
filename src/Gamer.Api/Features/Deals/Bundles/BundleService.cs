using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Deals;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Deals.Bundles;

public interface IBundleService
{
    Task<BundlePageDto?> QueryAsync(string? storeName, bool refresh, CancellationToken cancellationToken);
}

public class BundleService(GamerDbContext db, IEnumerable<IBundleSource> sources, BundleCache cache) : IBundleService
{
    private readonly Dictionary<string, IBundleSource> _sources = sources.ToDictionary(s => s.StoreName);

    public async Task<BundlePageDto?> QueryAsync(string? storeName, bool refresh, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storeName))
        {
            return null;
        }

        Store? store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.IsEnabled && s.Name == storeName, cancellationToken);
        if (store is null || !_sources.TryGetValue(store.Name, out IBundleSource? source))
        {
            return null;
        }

        BundleEntry entry = cache.EnsureLoaded(store, source, refresh);
        HashSet<string> library = (await db.SteamLibrary.AsNoTracking().Select(i => i.Title).ToListAsync(cancellationToken))
            .Select(TitleMatcher.Normalize).ToHashSet();
        HashSet<string> wishlist = (await db.Wishlist.AsNoTracking().Select(i => i.Title).ToListAsync(cancellationToken))
            .Select(TitleMatcher.Normalize).ToHashSet();

        List<BundleDto> bundles = entry.Bundles
            .Select(bundle => ToDto(store.Name, bundle, library, wishlist))
            .OrderBy(bundle => bundle.EndsAt ?? DateTimeOffset.MaxValue)
            .ToList();

        return new BundlePageDto(bundles, entry.Loading, entry.UpdatedAt, entry.Error);
    }

    private static BundleDto ToDto(string storeName, StoreBundle bundle, HashSet<string> library, HashSet<string> wishlist) =>
        new(storeName, bundle.Title, bundle.Url, bundle.ImageUrl, bundle.Blurb, bundle.EndsAt, bundle.Currency, bundle.Kind, bundle.Tiers,
            bundle.Items.Select(item =>
            {
                string key = TitleMatcher.Normalize(item.Title);
                return new BundleItemDto(item.Title, item.ImageUrl, item.Msrp, item.Tier, library.Contains(key), wishlist.Contains(key));
            }).ToList());
}
