namespace Gamer.Contracts.Games;

public record GameRequest(
    string Title,
    int PlatformId,
    GameStatus Status,
    bool OnDeck,
    int? Rating,
    string? Notes);
