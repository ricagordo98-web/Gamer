namespace Gamer.Contracts.Steam;

public record WishlistItemDto(long AppId, string Title, DateTimeOffset? AddedAt);
