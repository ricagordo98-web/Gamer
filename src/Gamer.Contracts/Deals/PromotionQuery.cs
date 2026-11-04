namespace Gamer.Contracts.Deals;

public static class PromotionSort
{
    public const string Discount = "discount";
    public const string Saving = "saving";
    public const string Price = "price";
    public const string Name = "name";
}

public record PromotionQuery(
    string? Store = null,
    string? Search = null,
    int? MinDiscount = null,
    decimal? MaxPrice = null,
    bool OnlyWishlist = false,
    bool HideOwned = false,
    bool HideExtras = true,
    bool MultiStore = false,
    string? Sort = PromotionSort.Discount,
    int Skip = 0,
    int Take = 48);
