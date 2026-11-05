namespace Gamer.Api.Domain;

public class CartItem
{
    public int Id { get; set; }
    public required string Kind { get; set; }
    public required string Store { get; set; }
    public required string Title { get; set; }
    public required string Url { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public List<CartGame> Games { get; set; } = [];
}

public class CartGame
{
    public required string Title { get; set; }
    public string? ImageUrl { get; set; }
}
