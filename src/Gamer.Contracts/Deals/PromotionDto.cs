namespace Gamer.Contracts.Deals;

public record PromotionDto(
    string Title,
    string? ImageUrl,
    OfferDto Best,
    decimal Saving,
    IReadOnlyList<OfferDto> Others,
    bool Owned,
    bool OnWishlist,
    bool IsExtra);
