using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Steam;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Stats;

public class SteamEnrichmentService(GamerDbContext db, ISteamStoreClient store, IHistoricLowClient historicLow, ISteamSettingsService settings, TimeProvider clock, ILogger<SteamEnrichmentService> logger)
{
    private const int MaxConsecutiveFailures = 5;
    private const int PriceBatchSize = 50;
    private static readonly TimeSpan Staleness = TimeSpan.FromDays(7);

    public async Task<int> PendingAsync(CancellationToken cancellationToken) =>
        (await FindPendingAsync(cancellationToken)).Count;

    public async Task RunAsync(TimeSpan delay, Action<int, int> report, CancellationToken cancellationToken)
    {
        if (!await RefreshPricesAsync(delay, report, cancellationToken))
        {
            return;
        }

        IReadOnlyList<long> pending = await FindPendingAsync(cancellationToken);
        string? itadKey = await settings.GetItadKeyAsync(cancellationToken);
        int done = 0;
        int failures = 0;
        report(done, pending.Count);

        foreach (long appId in pending)
        {
            try
            {
                SteamStoreInfo info = await store.GetInfoAsync(appId, cancellationToken);
                HistoricLow? low = itadKey is null ? null : await historicLow.GetAsync(itadKey, appId, cancellationToken);
                await SaveAsync(appId, info, low, cancellationToken);
                failures = 0;
            }
            catch (SteamException exception)
            {
                logger.LogWarning(exception, "Could not enrich Steam app {AppId}.", appId);
                if (++failures >= MaxConsecutiveFailures)
                {
                    return;
                }
            }

            report(++done, pending.Count);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private async Task<bool> RefreshPricesAsync(TimeSpan delay, Action<int, int> report, CancellationToken cancellationToken)
    {
        List<SteamAppInfo> rows = await db.SteamAppInfos.ToListAsync(cancellationToken);
        int done = 0;
        int failures = 0;
        report(done, rows.Count);

        foreach (SteamAppInfo[] batch in rows.Chunk(PriceBatchSize))
        {
            try
            {
                IReadOnlyDictionary<long, SteamPrice> prices = await store.GetPricesAsync(batch.Select(r => r.AppId).ToList(), cancellationToken);
                DateTimeOffset now = clock.GetUtcNow();
                foreach (SteamAppInfo row in batch.Where(r => prices.ContainsKey(r.AppId)))
                {
                    SteamPrice price = prices[row.AppId];
                    ApplyPrice(row, price.Price, price.NormalPrice, price.Currency, price.DiscountPercent, now);
                }

                await db.SaveChangesAsync(cancellationToken);
                failures = 0;
            }
            catch (SteamException exception)
            {
                logger.LogWarning(exception, "Could not refresh a batch of Steam prices.");
                if (++failures >= MaxConsecutiveFailures)
                {
                    return false;
                }
            }

            report(done += batch.Length, rows.Count);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }
        }

        return true;
    }

    private static void ApplyPrice(SteamAppInfo row, decimal? price, decimal? normalPrice, string currency, int discountPercent, DateTimeOffset now)
    {
        row.Price = price;
        row.NormalPrice = normalPrice;
        row.Currency = currency;
        row.DiscountPercent = discountPercent;
        row.PriceUpdatedAt = now;
        if (price is > 0 && (row.LowestSeenPrice is null || price < row.LowestSeenPrice))
        {
            row.LowestSeenPrice = price;
            row.LowestSeenAt = now;
        }
    }

    private async Task<IReadOnlyList<long>> FindPendingAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset limit = clock.GetUtcNow() - Staleness;
        List<long> library = await db.SteamLibrary.AsNoTracking().OrderByDescending(i => i.PlaytimeMinutes).Select(i => i.AppId).ToListAsync(cancellationToken);
        Dictionary<long, DateTimeOffset> known = (await db.SteamAppInfos.AsNoTracking().Select(i => new { i.AppId, i.UpdatedAt }).ToListAsync(cancellationToken))
            .ToDictionary(i => i.AppId, i => i.UpdatedAt);

        List<long> missing = library.Where(id => !known.ContainsKey(id)).ToList();
        List<long> stale = library.Where(id => known.TryGetValue(id, out DateTimeOffset at) && at < limit).OrderBy(id => known[id]).ToList();
        return [.. missing, .. stale];
    }

    private async Task SaveAsync(long appId, SteamStoreInfo info, HistoricLow? low, CancellationToken cancellationToken)
    {
        SteamAppInfo row = await db.SteamAppInfos.FirstOrDefaultAsync(i => i.AppId == appId, cancellationToken)
            ?? db.SteamAppInfos.Add(new SteamAppInfo { AppId = appId }).Entity;

        if (info.Details is { } details)
        {
            ApplyPrice(row, details.Price, details.NormalPrice, details.Currency, details.DiscountPercent, clock.GetUtcNow());
            row.Metacritic = details.Metacritic;
            row.Genres = string.Join(", ", details.Genres);
            row.ReleaseDate = details.ReleaseDate;
        }

        if (info.Reviews is { } reviews)
        {
            row.ReviewPercent = reviews.Percent;
            row.ReviewCount = reviews.Count;
            row.ReviewSummary = reviews.Summary;
        }

        if (info.Spy is { } spy)
        {
            row.Owners = spy.Owners;
            row.AverageMinutes = spy.AverageMinutes;
            row.Tags = string.Join(", ", spy.Tags);
        }

        if (low is not null)
        {
            row.LowestPrice = low.Price;
            row.LowestPriceShop = low.Shop;
        }

        row.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SteamEnrichmentRunner(IServiceScopeFactory scopes, ILogger<SteamEnrichmentRunner> logger)
{
    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(1500);

    private int _running;
    private int _done;
    private int _total;

    public Contracts.Stats.EnrichmentStatusDto Status => new(_running == 1, _done, _total);

    public bool TryStart()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return false;
        }

        _ = Task.Run(RunAsync);
        return true;
    }

    private async Task RunAsync()
    {
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            SteamEnrichmentService service = scope.ServiceProvider.GetRequiredService<SteamEnrichmentService>();
            await service.RunAsync(RequestDelay, (done, total) => (_done, _total) = (done, total), CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The Steam enrichment run failed.");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}

public sealed class PlayerCountCache(ISteamStoreClient store, TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private const int Parallelism = 4;

    private readonly Dictionary<long, (int? Players, DateTimeOffset At)> _entries = [];
    private readonly object _gate = new();

    public async Task<int?> GetAsync(long appId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(appId, out (int? Players, DateTimeOffset At) cached) && clock.GetUtcNow() - cached.At < Lifetime)
            {
                return cached.Players;
            }
        }

        int? players = await store.GetCurrentPlayersAsync(appId, cancellationToken);
        lock (_gate)
        {
            _entries[appId] = (players, clock.GetUtcNow());
        }

        return players;
    }

    public async Task<IReadOnlyList<Contracts.Stats.PlayersNowDto>> GetManyAsync(IEnumerable<long> appIds, CancellationToken cancellationToken)
    {
        using SemaphoreSlim limit = new(Parallelism);
        IEnumerable<Task<Contracts.Stats.PlayersNowDto>> tasks = appIds.Distinct().Select(async id =>
        {
            await limit.WaitAsync(cancellationToken);
            try
            {
                return new Contracts.Stats.PlayersNowDto(id, await GetAsync(id, cancellationToken));
            }
            finally
            {
                limit.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }
}
