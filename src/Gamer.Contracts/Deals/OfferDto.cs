namespace Gamer.Contracts.Deals;

public record OfferDto(string Store, string Title, decimal Price, decimal OriginalPrice, int DiscountPercent, string Url, string Currency, string? ImageUrl = null);
