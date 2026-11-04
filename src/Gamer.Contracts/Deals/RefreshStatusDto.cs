namespace Gamer.Contracts.Deals;

public record RefreshStatusDto(bool Running, DateTimeOffset? LastCompletedAt, DateTimeOffset? PromotionsCompletedAt = null);
