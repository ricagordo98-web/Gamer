namespace Gamer.Contracts.Deals;

public record PromotionPageDto(
    IReadOnlyList<PromotionDto> Items,
    int Total,
    IReadOnlyList<PromotionStoreDto> Stores,
    DateTimeOffset? UpdatedAt);
