namespace Gamer.Contracts.Deals;

public static class CartKinds
{
    public const string Game = "game";
    public const string Bundle = "bundle";
}

public static class CartStatuses
{
    public const string Active = "active";
    public const string EndingSoon = "ending";
    public const string Ended = "ended";
    public const string PriceUp = "priceUp";
    public const string PriceDown = "priceDown";
}

public record CartGameDto(string Title, string? ImageUrl);

public record CartItemRequest(
    string Kind,
    string Store,
    string Title,
    string Url,
    string? ImageUrl,
    decimal Price,
    decimal OriginalPrice,
    string Currency,
    DateTimeOffset? EndsAt,
    IReadOnlyList<CartGameDto>? Games);

public record CartItemDto(
    int Id,
    string Kind,
    string Store,
    string Title,
    string Url,
    string? ImageUrl,
    decimal Price,
    decimal OriginalPrice,
    string Currency,
    decimal? CurrentPrice,
    DateTimeOffset? EndsAt,
    DateTimeOffset AddedAt,
    string Status,
    IReadOnlyList<CartGameDto> Games,
    CheaperOfferDto? Cheaper = null);

public record CheaperOfferDto(string Store, string Url, decimal Price, decimal OriginalPrice, string Currency);
