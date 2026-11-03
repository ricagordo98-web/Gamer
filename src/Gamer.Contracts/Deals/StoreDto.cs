namespace Gamer.Contracts.Deals;

public record StoreDto(int Id, string Name, string BaseUrl, bool IsEnabled, string Region, string Currency);
