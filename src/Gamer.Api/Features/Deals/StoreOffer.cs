namespace Gamer.Api.Features.Deals;

public record StoreOffer(string Store, string Title, decimal Price, decimal OriginalPrice, string Url, string Currency, string? ImageUrl = null)
{
    public int DiscountPercent => OriginalPrice > Price && OriginalPrice > 0
        ? (int)Math.Round((OriginalPrice - Price) / OriginalPrice * 100m, MidpointRounding.AwayFromZero)
        : 0;
}
