namespace Gamer.Api.Domain;

public class WishlistItem
{
    public long AppId { get; set; }
    public required string Title { get; set; }
    public DateTimeOffset? AddedAt { get; set; }
}
