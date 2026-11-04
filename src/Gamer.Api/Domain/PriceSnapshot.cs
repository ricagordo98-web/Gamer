namespace Gamer.Api.Domain;

public class PriceSnapshot
{
    public int Id { get; set; }
    public required string TitleKey { get; set; }
    public int StoreId { get; set; }
    public Store Store { get; set; } = null!;
    public bool Available { get; set; }
    public string? Reason { get; set; }
    public string? OfferTitle { get; set; }
    public decimal Price { get; set; }
    public decimal OriginalPrice { get; set; }
    public string? Url { get; set; }
    public string? Currency { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
}
