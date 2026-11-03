namespace Gamer.Api.Domain;

public class TrackedGame
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string TitleKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
