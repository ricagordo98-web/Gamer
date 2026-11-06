namespace Gamer.Contracts.Steam;

public record SteamSyncResultDto(int LibraryCount, int WishlistCount, int NewGames = 0);
