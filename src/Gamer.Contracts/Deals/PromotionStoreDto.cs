namespace Gamer.Contracts.Deals;

public record PromotionStoreDto(string Store, int Count, DateTimeOffset? UpdatedAt, string? Error);
