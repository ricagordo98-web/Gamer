namespace Gamer.Contracts.Games;

public record GameDto(
    int Id,
    string Title,
    int PlatformId,
    string PlatformName,
    string PlatformFamily,
    PlatformFormat Format,
    GameStatus Status,
    bool OnDeck,
    int? Rating,
    string? Notes);
