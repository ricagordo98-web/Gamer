namespace Gamer.Api.Domain;

public class Store
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public bool IsEnabled { get; set; } = true;
    public required string Region { get; set; }
    public required string Currency { get; set; }
}
