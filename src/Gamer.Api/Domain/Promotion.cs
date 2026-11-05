namespace Gamer.Api.Domain;

public class Promotion
{
    public int Id { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public required string Title { get; set; }
    public required string TitleKey { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public int DiscountPercent { get; set; }
    public required string Url { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
}
