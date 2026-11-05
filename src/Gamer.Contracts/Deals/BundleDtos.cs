namespace Gamer.Contracts.Deals;

public static class BundleKinds
{
    public const string Tiered = "tiered";
    public const string Pick = "pick";
}

public record BundleTierDto(decimal Price, int Count);

public record BundleItemDto(string Title, string? ImageUrl, decimal? Msrp, int Tier, bool Owned, bool OnWishlist);

public record BundleDto(
    string Store,
    string Title,
    string Url,
    string? ImageUrl,
    string? Blurb,
    DateTimeOffset? EndsAt,
    string Currency,
    string Kind,
    IReadOnlyList<BundleTierDto> Tiers,
    IReadOnlyList<BundleItemDto> Items);

public record BundlePageDto(IReadOnlyList<BundleDto> Items, bool Loading, DateTimeOffset? UpdatedAt, string? Error);
