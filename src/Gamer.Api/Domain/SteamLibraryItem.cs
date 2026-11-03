namespace Gamer.Api.Domain;

public class SteamLibraryItem
{
    public long AppId { get; set; }
    public required string Title { get; set; }
    public int PlaytimeMinutes { get; set; }
}
