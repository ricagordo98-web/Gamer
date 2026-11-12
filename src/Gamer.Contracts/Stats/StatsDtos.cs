namespace Gamer.Contracts.Stats;

public record StatsGameDto(long AppId, string Title, int Minutes, int RecentMinutes, DateTimeOffset? LastPlayedAt, decimal? Price, decimal? NormalPrice, int? ReviewPercent, string? ReviewSummary);

public record StatsCountDto(string Name, int Count, int Minutes);

public record EnrichmentStatusDto(bool Running, int Done, int Total);

public record StatsDto(
    int SteamGames,
    int PlayedGames,
    int NeverPlayed,
    int TotalMinutes,
    int RecentMinutes,
    decimal ValueNormal,
    decimal ValueCurrent,
    string Currency,
    int PricedGames,
    int InfoGames,
    IReadOnlyList<StatsGameDto> TopPlayed,
    IReadOnlyList<StatsGameDto> Recent,
    IReadOnlyList<StatsGameDto> NeverPlayedTop,
    IReadOnlyList<StatsCountDto> Genres,
    EnrichmentStatusDto Enrichment,
    decimal NeverPlayedValue,
    IReadOnlyList<StatsCountDto> PlaytimeBuckets,
    IReadOnlyList<StatsCountDto> Recency,
    IReadOnlyList<StatsCountDto> Tags,
    IReadOnlyList<StatsGameDto> BestRated,
    IReadOnlyList<StatsGameDto> BestValue);

public record StatsRowDto(
    long AppId,
    string Title,
    int Minutes,
    int RecentMinutes,
    DateTimeOffset? LastPlayedAt,
    decimal? Price,
    decimal? NormalPrice,
    int DiscountPercent,
    int? ReviewPercent,
    int ReviewCount,
    int? Metacritic,
    string Genres);

public record SteamAppDetailDto(
    long AppId,
    string Title,
    int Minutes,
    int RecentMinutes,
    DateTimeOffset? LastPlayedAt,
    bool HasInfo,
    decimal? Price,
    decimal? NormalPrice,
    string Currency,
    int DiscountPercent,
    int? Metacritic,
    int? ReviewPercent,
    int ReviewCount,
    string? ReviewSummary,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Tags,
    string? ReleaseDate,
    string? Owners,
    int? AverageMinutes,
    decimal? LowestPrice,
    string? LowestPriceShop,
    bool AtHistoricLow);

public record PlayersNowDto(long AppId, int? Players);
