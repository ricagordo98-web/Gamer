using System.Collections.Concurrent;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;

namespace Gamer.Api.Features.Cart;

/// <summary>
/// Looks up each cart game in every enabled store in the background and remembers what it found,
/// so the cart can warn when the same game is cheaper elsewhere without waiting for the scrapers.
/// </summary>
public class CartPriceComparer(StoreSearcher searcher, TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(3);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly Lock _gate = new();

    private record Entry(IReadOnlyList<StoreOffer> Offers, DateTimeOffset? UpdatedAt, bool Loading);

    public IReadOnlyList<StoreOffer> Get(string titleKey) => _entries.TryGetValue(titleKey, out Entry? entry) ? entry.Offers : [];

    public void EnsureLoaded(string title, string titleKey, IReadOnlyList<Store> stores)
    {
        lock (_gate)
        {
            Entry current = _entries.GetValueOrDefault(titleKey) ?? new Entry([], null, false);
            bool stale = current.UpdatedAt is null || time.GetUtcNow() - current.UpdatedAt > Lifetime;
            if (current.Loading || !stale || stores.Count == 0)
            {
                return;
            }

            _entries[titleKey] = current with { Loading = true };
            _ = Task.Run(() => LoadAsync(title, titleKey, stores));
        }
    }

    private async Task LoadAsync(string title, string titleKey, IReadOnlyList<Store> stores)
    {
        Entry previous = _entries.GetValueOrDefault(titleKey) ?? new Entry([], null, true);
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(90));
            IReadOnlyList<StoreOutcome> outcomes = await searcher.SearchAsync(stores, title, timeout.Token);
            List<StoreOffer> offers = outcomes.Where(o => o.Offer is not null).Select(o => o.Offer!).ToList();
            _entries[titleKey] = new Entry(offers, time.GetUtcNow(), false);
        }
        catch (Exception)
        {
            _entries[titleKey] = previous with { Loading = false, UpdatedAt = time.GetUtcNow() - Lifetime + TimeSpan.FromMinutes(10) };
        }
    }
}
