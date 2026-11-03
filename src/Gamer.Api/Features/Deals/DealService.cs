using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Deals;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Deals;

public interface IDealService
{
    Task<DealDto> SearchAsync(string query, CancellationToken cancellationToken);
    Task<IReadOnlyList<DealDto>> GetDealsAsync(CancellationToken cancellationToken);
    Task<DealDto> TrackAsync(string title, CancellationToken cancellationToken);
    Task<bool> UntrackAsync(int id, CancellationToken cancellationToken);
    Task RefreshAsync(CancellationToken cancellationToken);
}

public class DealService(GamerDbContext db, StoreSearcher searcher, TimeProvider time) : IDealService
{
    public async Task<DealDto> SearchAsync(string query, CancellationToken cancellationToken)
    {
        List<Store> stores = await EnabledStoresAsync(cancellationToken);
        IReadOnlyList<StoreOutcome> outcomes = await searcher.SearchAsync(stores, query.Trim(), cancellationToken);
        Ownership ownership = await LoadOwnershipAsync(cancellationToken);

        string title = outcomes.FirstOrDefault(o => o.Offer is not null)?.Offer!.Title ?? query.Trim();
        return Build(query.Trim(), null, ownership, outcomes.Where(o => o.Offer is not null).Select(o => o.Offer!).ToList(), ToStatuses(outcomes), title);
    }

    public async Task<IReadOnlyList<DealDto>> GetDealsAsync(CancellationToken cancellationToken)
    {
        List<TitleEntry> entries = await LoadTitlesAsync(cancellationToken);
        Ownership ownership = await LoadOwnershipAsync(cancellationToken);
        List<PriceSnapshot> snapshots = await db.PriceSnapshots.AsNoTracking().Include(s => s.Store).ToListAsync(cancellationToken);
        ILookup<string, PriceSnapshot> byTitle = snapshots.ToLookup(s => s.TitleKey);

        return entries
            .Select(entry => BuildFromSnapshots(entry, byTitle[entry.Key].ToList(), ownership))
            .OrderByDescending(d => d.Best is not null)
            .ThenByDescending(d => d.Best?.DiscountPercent ?? 0)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<DealDto> TrackAsync(string title, CancellationToken cancellationToken)
    {
        string trimmed = title.Trim();
        string key = TitleMatcher.Normalize(trimmed);
        TrackedGame? tracked = await db.TrackedGames.FirstOrDefaultAsync(t => t.TitleKey == key, cancellationToken);
        if (tracked is null)
        {
            tracked = new TrackedGame { Title = trimmed, TitleKey = key, CreatedAt = time.GetUtcNow() };
            db.TrackedGames.Add(tracked);
            await db.SaveChangesAsync(cancellationToken);
        }

        TitleEntry entry = new(tracked.Title, key, tracked.Id, null, false);
        await RefreshTitleAsync(entry, await EnabledStoresAsync(cancellationToken), cancellationToken);

        Ownership ownership = await LoadOwnershipAsync(cancellationToken);
        List<PriceSnapshot> snapshots = await db.PriceSnapshots.AsNoTracking().Include(s => s.Store)
            .Where(s => s.TitleKey == key).ToListAsync(cancellationToken);
        return BuildFromSnapshots(entry, snapshots, ownership);
    }

    public async Task<bool> UntrackAsync(int id, CancellationToken cancellationToken)
    {
        TrackedGame? tracked = await db.TrackedGames.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tracked is null)
        {
            return false;
        }

        db.TrackedGames.Remove(tracked);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        List<Store> stores = await EnabledStoresAsync(cancellationToken);
        foreach (TitleEntry entry in await LoadTitlesAsync(cancellationToken))
        {
            await RefreshTitleAsync(entry, stores, cancellationToken);
        }
    }

    private async Task RefreshTitleAsync(TitleEntry entry, IReadOnlyList<Store> stores, CancellationToken cancellationToken)
    {
        IReadOnlyList<StoreOutcome> outcomes = await searcher.SearchAsync(stores, entry.Title, cancellationToken);
        Dictionary<int, PriceSnapshot> existing = await db.PriceSnapshots
            .Where(s => s.TitleKey == entry.Key)
            .ToDictionaryAsync(s => s.StoreId, cancellationToken);

        foreach (StoreOutcome outcome in outcomes)
        {
            if (!existing.TryGetValue(outcome.Store.Id, out PriceSnapshot? snapshot))
            {
                snapshot = new PriceSnapshot { TitleKey = entry.Key, StoreId = outcome.Store.Id };
                db.PriceSnapshots.Add(snapshot);
            }

            snapshot.Available = outcome.Offer is not null;
            snapshot.Reason = outcome.FailureReason;
            snapshot.OfferTitle = outcome.Offer?.Title;
            snapshot.Price = outcome.Offer?.Price ?? 0;
            snapshot.OriginalPrice = outcome.Offer?.OriginalPrice ?? 0;
            snapshot.Url = outcome.Offer?.Url;
            snapshot.Currency = outcome.Offer?.Currency;
            snapshot.CapturedAt = time.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<List<Store>> EnabledStoresAsync(CancellationToken cancellationToken) =>
        db.Stores.AsNoTracking().Where(s => s.IsEnabled).OrderBy(s => s.Id).ToListAsync(cancellationToken);

    private async Task<List<TitleEntry>> LoadTitlesAsync(CancellationToken cancellationToken)
    {
        List<WishlistItem> wishlist = await db.Wishlist.AsNoTracking().ToListAsync(cancellationToken);
        List<TrackedGame> tracked = await db.TrackedGames.AsNoTracking().ToListAsync(cancellationToken);

        Dictionary<string, TitleEntry> entries = [];
        foreach (WishlistItem item in wishlist)
        {
            string key = TitleMatcher.Normalize(item.Title);
            entries.TryAdd(key, new TitleEntry(item.Title, key, null, item.AppId, true));
        }

        foreach (TrackedGame item in tracked)
        {
            entries[item.TitleKey] = entries.TryGetValue(item.TitleKey, out TitleEntry? existing)
                ? existing with { TrackedId = item.Id }
                : new TitleEntry(item.Title, item.TitleKey, item.Id, null, false);
        }

        return entries.Values.ToList();
    }

    private async Task<Ownership> LoadOwnershipAsync(CancellationToken cancellationToken)
    {
        List<string> library = await db.SteamLibrary.AsNoTracking().Select(i => i.Title).ToListAsync(cancellationToken);
        return new Ownership(library.Select(TitleMatcher.Normalize).ToHashSet());
    }

    private static DealDto BuildFromSnapshots(TitleEntry entry, List<PriceSnapshot> snapshots, Ownership ownership)
    {
        List<StoreOffer> offers = snapshots
            .Where(s => s.Available && s.Url is not null && s.Currency is not null)
            .Select(s => new StoreOffer(s.Store.Name, s.OfferTitle ?? entry.Title, s.Price, s.OriginalPrice, s.Url!, s.Currency!))
            .ToList();
        List<StoreStatusDto> statuses = snapshots
            .OrderBy(s => s.StoreId)
            .Select(s => new StoreStatusDto(s.Store.Name, s.Available, s.Reason))
            .ToList();

        return Build(entry.Title, entry, ownership, offers, statuses, entry.Title);
    }

    private static DealDto Build(string title, TitleEntry? entry, Ownership ownership, List<StoreOffer> offers, IReadOnlyList<StoreStatusDto> statuses, string matchTitle)
    {
        bool owned = ownership.LibraryKeys.Contains(TitleMatcher.Normalize(matchTitle))
            || ownership.LibraryKeys.Contains(TitleMatcher.Normalize(title));
        DealDto deal = DealBuilder.Build(title, entry?.TrackedId, entry?.SteamAppId, owned, entry?.OnWishlist ?? false, offers, statuses);
        return deal;
    }

    private static IReadOnlyList<StoreStatusDto> ToStatuses(IReadOnlyList<StoreOutcome> outcomes) =>
        outcomes.Select(o => new StoreStatusDto(o.Store.Name, o.Offer is not null, o.FailureReason)).ToList();

    private record TitleEntry(string Title, string Key, int? TrackedId, long? SteamAppId, bool OnWishlist);

    private record Ownership(HashSet<string> LibraryKeys);
}
