using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Stats;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Stats;

public class StatsService(GamerDbContext db, SteamEnrichmentRunner runner)
{
    private const int ListSize = 10;
    private const int GenreCount = 8;
    private const char ListSeparator = ',';

    public async Task<StatsDto> GetAsync(CancellationToken cancellationToken)
    {
        List<SteamLibraryItem> library = await db.SteamLibrary.AsNoTracking().ToListAsync(cancellationToken);
        Dictionary<long, SteamAppInfo> infos = await db.SteamAppInfos.AsNoTracking().ToDictionaryAsync(i => i.AppId, cancellationToken);

        StatsGameDto ToDto(SteamLibraryItem item)
        {
            infos.TryGetValue(item.AppId, out SteamAppInfo? info);
            return new StatsGameDto(item.AppId, item.Title, item.PlaytimeMinutes, item.RecentMinutes, item.LastPlayedAt, info?.Price, info?.NormalPrice, info?.ReviewPercent, info?.ReviewSummary);
        }

        string currency = infos.Values.Where(i => i.Currency.Length > 0).GroupBy(i => i.Currency).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? string.Empty;
        List<SteamAppInfo> priced = infos.Values.Where(i => i.Price is not null && (i.Currency.Length == 0 || i.Currency == currency)).ToList();

        List<StatsCountDto> genres = library
            .Where(i => infos.ContainsKey(i.AppId))
            .SelectMany(i => infos[i.AppId].Genres.Split(ListSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(g => (Genre: g, i.PlaytimeMinutes)))
            .GroupBy(x => x.Genre)
            .Select(g => new StatsCountDto(g.Key, g.Count(), g.Sum(x => x.PlaytimeMinutes)))
            .OrderByDescending(g => g.Count)
            .Take(GenreCount)
            .ToList();

        return new StatsDto(
            library.Count,
            library.Count(i => i.PlaytimeMinutes > 0),
            library.Count(i => i.PlaytimeMinutes == 0),
            library.Sum(i => i.PlaytimeMinutes),
            library.Sum(i => i.RecentMinutes),
            priced.Sum(i => i.NormalPrice ?? 0m),
            priced.Sum(i => i.Price ?? 0m),
            currency,
            priced.Count,
            library.Count(i => infos.ContainsKey(i.AppId)),
            library.OrderByDescending(i => i.PlaytimeMinutes).Take(ListSize).Select(ToDto).ToList(),
            library.Where(i => i.RecentMinutes > 0).OrderByDescending(i => i.RecentMinutes).Take(ListSize).Select(ToDto).ToList(),
            library.Where(i => i.PlaytimeMinutes == 0).OrderByDescending(i => infos.TryGetValue(i.AppId, out SteamAppInfo? info) ? info.NormalPrice ?? 0m : 0m).ThenBy(i => i.Title).Take(ListSize).Select(ToDto).ToList(),
            genres,
            runner.Status);
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

    private static IReadOnlyList<string> Split(string? list) =>
        string.IsNullOrWhiteSpace(list) ? [] : list.Split(ListSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
