namespace Gamer.Contracts.Deals;

public record CatalogQuery(
    string? Store,
    string? Search = null,
    bool OnlySale = false,
    bool HideOwned = false,
    bool HideExtras = true,
    int Skip = 0,
    int Take = 48);

public record CatalogPageDto(
    IReadOnlyList<PromotionDto> Items,
    int Total,
    int OnSale,
    bool Loading,
    DateTimeOffset? UpdatedAt,
    string? Error);
