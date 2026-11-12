using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Stats;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Stats;

public class StatsService(GamerDbContext db, SteamEnrichmentRunner runner, TimeProvider clock)
{
    private const int ListSize = 10;
    private const int GenreCount = 8;
    private const int TagCount = 12;
    private const int MinReviewsForRanking = 100;
    private const int MinMinutesForValue = 60;
    private const char ListSeparator = ',';

    private static readonly (string Name, int From, int To)[] PlaytimeRanges =
    [
        ("Nunca", 0, 1),
        ("Menos de 2 h", 1, 120),
        ("2 a 10 h", 120, 600),
        ("10 a 50 h", 600, 3000),
        ("Mais de 50 h", 3000, int.MaxValue),
    ];

    public async Task<StatsDto> GetAsync(CancellationToken cancellationToken)
    {
        List<SteamLibraryItem> library = await db.SteamLibrary.AsNoTracking().ToListAsync(cancellationToken);
        Dictionary<long, SteamAppInfo> infos = await db.SteamAppInfos.AsNoTracking().ToDictionaryAsync(i => i.AppId, cancellationToken);
        DateTimeOffset now = clock.GetUtcNow();

        StatsGameDto ToDto(SteamLibraryItem item)
        {
            infos.TryGetValue(item.AppId, out SteamAppInfo? info);
            return new StatsGameDto(item.AppId, item.Title, item.PlaytimeMinutes, item.RecentMinutes, item.LastPlayedAt, info?.Price, info?.NormalPrice, info?.ReviewPercent, info?.ReviewSummary);
        }

        decimal NormalPriceOf(SteamLibraryItem item) => infos.TryGetValue(item.AppId, out SteamAppInfo? info) ? info.NormalPrice ?? 0m : 0m;

        string currency = infos.Values.Where(i => i.Currency.Length > 0).GroupBy(i => i.Currency).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? string.Empty;
        List<SteamAppInfo> priced = infos.Values.Where(i => i.Price is not null && (i.Currency.Length == 0 || i.Currency == currency)).ToList();
        List<SteamLibraryItem> neverPlayed = library.Where(i => i.PlaytimeMinutes == 0).ToList();

        List<(string Name, int Minutes)> genreEntries = library
            .Where(i => infos.ContainsKey(i.AppId))
            .SelectMany(i => Split(infos[i.AppId].Genres).Select(g => (g, i.PlaytimeMinutes)))
            .ToList();
        List<(string Name, int Minutes)> tagEntries = library
            .Where(i => infos.ContainsKey(i.AppId))
            .SelectMany(i => Split(infos[i.AppId].Tags).Select(t => (t, i.PlaytimeMinutes)))
            .ToList();

        List<StatsGameDto> bestRated = library
            .Where(i => infos.TryGetValue(i.AppId, out SteamAppInfo? info) && info.ReviewPercent is not null && info.ReviewCount >= MinReviewsForRanking)
            .OrderByDescending(i => infos[i.AppId].ReviewPercent)
            .ThenByDescending(i => infos[i.AppId].ReviewCount)
            .Take(ListSize)
            .Select(ToDto)
            .ToList();

        List<StatsGameDto> bestValue = library
            .Where(i => i.PlaytimeMinutes >= MinMinutesForValue && infos.TryGetValue(i.AppId, out SteamAppInfo? info) && info.Price > 0)
            .OrderBy(i => (double)(infos[i.AppId].Price ?? 0m) / i.PlaytimeMinutes)
            .Take(ListSize)
            .Select(ToDto)
            .ToList();

        return new StatsDto(
            library.Count,
            library.Count(i => i.PlaytimeMinutes > 0),
            neverPlayed.Count,
            library.Sum(i => i.PlaytimeMinutes),
            library.Sum(i => i.RecentMinutes),
            priced.Sum(i => i.NormalPrice ?? 0m),
            priced.Sum(i => i.Price ?? 0m),
            currency,
            priced.Count,
            library.Count(i => infos.ContainsKey(i.AppId)),
            library.OrderByDescending(i => i.PlaytimeMinutes).Take(ListSize).Select(ToDto).ToList(),
            library.Where(i => i.RecentMinutes > 0).OrderByDescending(i => i.RecentMinutes).Take(ListSize).Select(ToDto).ToList(),
            neverPlayed.OrderByDescending(NormalPriceOf).ThenBy(i => i.Title).Take(ListSize).Select(ToDto).ToList(),
            Count(genreEntries, GenreCount),
            runner.Status,
            neverPlayed.Sum(NormalPriceOf),
            PlaytimeRanges.Select(r => RangeCount(r.Name, library.Where(i => i.PlaytimeMinutes >= r.From && i.PlaytimeMinutes < r.To).ToList())).ToList(),
            Recency(library, now),
            Count(tagEntries, TagCount),
            bestRated,
            bestValue);
    }

    public async Task<IReadOnlyList<StatsRowDto>> GetGamesAsync(CancellationToken cancellationToken)
    {
        List<SteamLibraryItem> library = await db.SteamLibrary.AsNoTracking().OrderBy(i => i.Title).ToListAsync(cancellationToken);
        Dictionary<long, SteamAppInfo> infos = await db.SteamAppInfos.AsNoTracking().ToDictionaryAsync(i => i.AppId, cancellationToken);

        return library.Select(item =>
        {
            infos.TryGetValue(item.AppId, out SteamAppInfo? info);
            return new StatsRowDto(item.AppId, item.Title, item.PlaytimeMinutes, item.RecentMinutes, item.LastPlayedAt, info?.Price, info?.NormalPrice,
                info?.DiscountPercent ?? 0, info?.ReviewPercent, info?.ReviewCount ?? 0, info?.Metacritic, info?.Genres ?? string.Empty);
        }).ToList();
    }

    public async Task<SteamAppDetailDto?> GetAppAsync(long appId, CancellationToken cancellationToken)
    {
        SteamLibraryItem? item = await db.SteamLibrary.AsNoTracking().FirstOrDefaultAsync(i => i.AppId == appId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        SteamAppInfo? info = await db.SteamAppInfos.AsNoTracking().FirstOrDefaultAsync(i => i.AppId == appId, cancellationToken);
        return new SteamAppDetailDto(
            item.AppId,
            item.Title,
            item.PlaytimeMinutes,
            item.RecentMinutes,
            item.LastPlayedAt,
            info is not null,
            info?.Price,
            info?.NormalPrice,
            info?.Currency ?? string.Empty,
            info?.DiscountPercent ?? 0,
            info?.Metacritic,
            info?.ReviewPercent,
            info?.ReviewCount ?? 0,
            info?.ReviewSummary,
            Split(info?.Genres),
            Split(info?.Tags),
            info?.ReleaseDate,
            info?.Owners,
            info?.AverageMinutes,
            info?.LowestPrice,
            info?.LowestPriceShop,
            info is { Price: { } price, LowestPrice: { } lowest } && price <= lowest);
    }

    private static List<StatsCountDto> Count(List<(string Name, int Minutes)> entries, int take) =>
        entries
            .GroupBy(e => e.Name)
            .Select(g => new StatsCountDto(g.Key, g.Count(), g.Sum(e => e.Minutes)))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .Take(take)
            .ToList();

    private static StatsCountDto RangeCount(string name, List<SteamLibraryItem> items) =>
        new(name, items.Count, items.Sum(i => i.PlaytimeMinutes));

    private static List<StatsCountDto> Recency(List<SteamLibraryItem> library, DateTimeOffset now)
    {
        List<SteamLibraryItem> played = library.Where(i => i.PlaytimeMinutes > 0).ToList();
        (string Name, TimeSpan Within)[] windows =
        [
            ("Últimos 7 dias", TimeSpan.FromDays(7)),
            ("Últimos 30 dias", TimeSpan.FromDays(30)),
            ("Últimos 6 meses", TimeSpan.FromDays(183)),
            ("Último ano", TimeSpan.FromDays(365)),
        ];

        List<StatsCountDto> result = [];
        List<SteamLibraryItem> remaining = played;
        foreach ((string name, TimeSpan within) in windows)
        {
            List<SteamLibraryItem> bucket = remaining.Where(i => i.LastPlayedAt is { } at && now - at <= within).ToList();
            result.Add(RangeCount(name, bucket));
            remaining = remaining.Except(bucket).ToList();
        }

        result.Add(RangeCount("Há mais tempo", remaining));
        return result;
    }

    private static IReadOnlyList<string> Split(string? list) =>
        string.IsNullOrWhiteSpace(list) ? [] : list.Split(ListSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
