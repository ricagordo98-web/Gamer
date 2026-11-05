using Gamer.Contracts.Auth;
using Gamer.Contracts.Deals;
using Gamer.Contracts.Games;
using Gamer.Contracts.Steam;

namespace Gamer.Ui.Services;

public interface IGamerApi
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PlatformDto>> GetPlatformsAsync(CancellationToken cancellationToken);
    Task<GameDto> CreateGameAsync(GameRequest request, CancellationToken cancellationToken);
    Task<GameDto> UpdateGameAsync(int id, GameRequest request, CancellationToken cancellationToken);
    Task<GameDto> ChangeStatusAsync(int id, GameStatus status, CancellationToken cancellationToken);
    Task DeleteGameAsync(int id, CancellationToken cancellationToken);
    Task<ImportResultDto> ImportAsync(Stream workbook, string fileName, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoreDto>> GetStoresAsync(CancellationToken cancellationToken);
    Task<StoreDto> UpdateStoreAsync(int id, StoreUpdateRequest request, CancellationToken cancellationToken);
    Task<SteamSettingsDto> GetSteamSettingsAsync(CancellationToken cancellationToken);
    Task<SteamSettingsDto> SaveSteamSettingsAsync(SteamSettingsRequest request, CancellationToken cancellationToken);
    Task<SteamSyncResultDto> SyncSteamAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SteamLibraryItemDto>> GetSteamLibraryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<WishlistItemDto>> GetWishlistAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DealDto>> GetDealsAsync(CancellationToken cancellationToken);
    Task<DealDto> SearchDealAsync(string query, CancellationToken cancellationToken);
    Task<DealDto> TrackGameAsync(string title, CancellationToken cancellationToken);
    Task UntrackGameAsync(int id, CancellationToken cancellationToken);
    Task<RefreshStatusDto> RefreshDealsAsync(CancellationToken cancellationToken);
    Task<RefreshStatusDto> GetRefreshStatusAsync(CancellationToken cancellationToken);
    Task<PromotionPageDto> GetPromotionsAsync(PromotionQuery query, CancellationToken cancellationToken);
    Task<RefreshStatusDto> RefreshPromotionsAsync(CancellationToken cancellationToken);
    Task<CatalogPageDto> GetCatalogAsync(CatalogQuery query, bool refresh, CancellationToken cancellationToken);
    Task<BundlePageDto> GetBundlesAsync(string store, bool refresh, CancellationToken cancellationToken);
    Task<IReadOnlyList<CartItemDto>> GetCartAsync(CancellationToken cancellationToken);
    Task<CartItemDto> SaveCartItemAsync(CartItemRequest request, CancellationToken cancellationToken);
    Task DeleteCartItemAsync(int id, CancellationToken cancellationToken);
    Task<bool> PingAsync(CancellationToken cancellationToken);
}
