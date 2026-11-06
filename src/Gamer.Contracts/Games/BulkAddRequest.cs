namespace Gamer.Contracts.Games;

public record BulkAddRequest(int PlatformId, GameStatus Status, IReadOnlyList<string> Titles);
