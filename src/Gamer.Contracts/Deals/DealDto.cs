namespace Gamer.Contracts.Deals;

public record DealDto(
    string Title,
    int? TrackedId,
    long? SteamAppId,
    bool Owned,
    bool OnWishlist,
    OfferDto? Best,
    decimal? Saving,
    IReadOnlyList<OfferDto> Offers,
    IReadOnlyList<StoreStatusDto> Stores);
