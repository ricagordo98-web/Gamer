namespace Gamer.Api.Domain;

public class SteamAppInfo
{
    public long AppId { get; set; }
    public decimal? Price { get; set; }
    public decimal? NormalPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int DiscountPercent { get; set; }
    public int? Metacritic { get; set; }
    public int? ReviewPercent { get; set; }
    public int ReviewCount { get; set; }
    public string? ReviewSummary { get; set; }
    public string Genres { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string? ReleaseDate { get; set; }
    public string? Owners { get; set; }
    public int? AverageMinutes { get; set; }
    public decimal? LowestPrice { get; set; }
    public string? LowestPriceShop { get; set; }
    public decimal? LowestSeenPrice { get; set; }
    public DateTimeOffset? LowestSeenAt { get; set; }
    public DateTimeOffset? PriceUpdatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
