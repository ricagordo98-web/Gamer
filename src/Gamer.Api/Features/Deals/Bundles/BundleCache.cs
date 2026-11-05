using System.Collections.Concurrent;
using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Bundles;

public record BundleEntry(IReadOnlyList<StoreBundle> Bundles, DateTimeOffset? UpdatedAt, bool Loading, string? Error)
{
    public static BundleEntry Empty { get; } = new([], null, false, null);
}

public class BundleCache(TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(3);

    private readonly ConcurrentDictionary<string, BundleEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _gate = new();

    public BundleEntry Get(string storeName) => _entries.GetValueOrDefault(storeName) ?? BundleEntry.Empty;

    public BundleEntry EnsureLoaded(Store store, IBundleSource source, bool force)
    {
        lock (_gate)
        {
            BundleEntry current = Get(store.Name);
            bool stale = current.UpdatedAt is null || time.GetUtcNow() - current.UpdatedAt > Lifetime;
            if (current.Loading || !(force || stale || (current.Error is not null && current.Bundles.Count == 0)))
            {
                return current;
            }

            BundleEntry loading = current with { Loading = true, Error = null };
            _entries[store.Name] = loading;
            _ = Task.Run(() => LoadAsync(store, source));
            return loading;
        }
    }

    private async Task LoadAsync(Store store, IBundleSource source)
    {
        BundleEntry previous = Get(store.Name);
        try
        {
            IReadOnlyList<StoreBundle> bundles = await source.ListAsync(store, CancellationToken.None);
            _entries[store.Name] = bundles.Count > 0
                ? new BundleEntry(bundles, time.GetUtcNow(), false, null)
                : previous with { Loading = false, Error = "The store has no bundles right now." };
        }
        catch (Exception exception)
        {
            _entries[store.Name] = previous with { Loading = false, Error = exception.Message };
        }
    }
}
