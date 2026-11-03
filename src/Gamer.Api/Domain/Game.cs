using Gamer.Contracts.Games;

namespace Gamer.Api.Domain;

public class Game
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public int PlatformId { get; set; }
    public Platform Platform { get; set; } = null!;
    public GameStatus Status { get; set; }
    public bool OnDeck { get; set; }
    public int? Rating { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
