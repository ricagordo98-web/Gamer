using System.Net.Http.Json;
using System.Text.Json;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Deals;
using Gamer.Contracts.Games;
using Gamer.Contracts.Library;
using Gamer.Contracts.Steam;

namespace Gamer.Ui.Services;

public class GamerApiClient(HttpClient http) : IGamerApi
{
    public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        SendAsync<LoginResponse>(() => http.PostAsJsonAsync("api/auth/login", request, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<GameDto>> GetGamesAsync(CancellationToken cancellationToken) =>
        await SendAsync<List<GameDto>>(() => http.GetAsync("api/games", cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<PlatformDto>> GetPlatformsAsync(CancellationToken cancellationToken) =>
        await SendAsync<List<PlatformDto>>(() => http.GetAsync("api/platforms", cancellationToken), cancellationToken);

    public Task<GameDto> CreateGameAsync(GameRequest request, CancellationToken cancellationToken) =>
        SendAsync<GameDto>(() => http.PostAsJsonAsync("api/games", request, cancellationToken), cancellationToken);

    public Task<GameDto> UpdateGameAsync(int id, GameRequest request, CancellationToken cancellationToken) =>
        SendAsync<GameDto>(() => http.PutAsJsonAsync($"api/games/{id}", request, cancellationToken), cancellationToken);

    public Task<GameDto> ChangeStatusAsync(int id, GameStatus status, CancellationToken cancellationToken) =>
        SendAsync<GameDto>(() => http.PatchAsJsonAsync($"api/games/{id}/status", new StatusRequest(status), cancellationToken), cancellationToken);

    public async Task DeleteGameAsync(int id, CancellationToken cancellationToken) =>
        await SendAsync(() => http.DeleteAsync($"api/games/{id}", cancellationToken), cancellationToken);

    public Task<ImportResultDto> BulkAddGamesAsync(BulkAddRequest request, CancellationToken cancellationToken) =>
        SendAsync<ImportResultDto>(() => http.PostAsJsonAsync("api/games/bulk", request, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<LibrarySourceDto>> GetLibrarySourcesAsync(CancellationToken cancellationToken) =>
        await SendAsync<List<LibrarySourceDto>>(() => http.GetAsync("api/library/sources", cancellationToken), cancellationToken);

    public Task<LibrarySyncResultDto> SyncLibrarySourceAsync(string id, CancellationToken cancellationToken) =>
        SendAsync<LibrarySyncResultDto>(() => http.PostAsync($"api/library/sources/{Uri.EscapeDataString(id)}/sync", null, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<LibrarySourceDto>> SaveEmulatorPathAsync(string path, CancellationToken cancellationToken) =>
        await SendAsync<List<LibrarySourceDto>>(() => http.PutAsJsonAsync("api/library/sources/emulator", new EmulatorPathRequest(path), cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<LibrarySourceDto>> SaveSourcePathAsync(string id, string path, CancellationToken cancellationToken) =>
        await SendAsync<List<LibrarySourceDto>>(() => http.PutAsJsonAsync($"api/library/sources/{Uri.EscapeDataString(id)}/path", new EmulatorPathRequest(path), cancellationToken), cancellationToken);

    public Task<ImportResultDto> ImportAsync(Stream workbook, string fileName, CancellationToken cancellationToken)
    {
        MultipartFormDataContent form = new() { { new StreamContent(workbook), "file", fileName } };
        return SendAsync<ImportResultDto>(() => http.PostAsync("api/import", form, cancellationToken), cancellationToken);
    }

    public async Task<IReadOnlyList<StoreDto>> GetStoresAsync(CancellationToken cancellationToken) =>
        await SendAsync<List<StoreDto>>(() => http.GetAsync("api/stores", cancellationToken), cancellationToken);

    public Task<StoreDto> UpdateStoreAsync(int id, StoreUpdateRequest request, CancellationToken cancellationToken) =>
        SendAsync<StoreDto>(() => http.PutAsJsonAsync($"api/stores/{id}", request, cancellationToken), cancellationToken);

    public Task<SteamSettingsDto> GetSteamSettingsAsync(CancellationToken cancellationToken) =>
        SendAsync<SteamSettingsDto>(() => http.GetAsync("api/steam/settings", cancellationToken), cancellationToken);

    public Task<SteamSettingsDto> SaveSteamSettingsAsync(SteamSettingsRequest request, CancellationToken cancellationToken) =>
        SendAsync<SteamSettingsDto>(() => http.PutAsJsonAsync("api/steam/settings", request, cancellationToken), cancellationToken);

    public Task<SteamSyncResultDto> SyncSteamAsync(CancellationToken cancellationToken) =>
        SendAsync<SteamSyncResultDto>(() => http.PostAsync("api/steam/sync", null, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<SteamLibraryItemDto>> GetSteamLibraryAsync(CancellationToken cancellationToken) =>
        await SendAsync<List<SteamLibraryItemDto>>(() => http.GetAsync("api/steam/library", cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<WishlistItemDto>> GetWishlistAsync(CancellationToken cancellationToken) =>
        await SendAsync<List<WishlistItemDto>>(() => http.GetAsync("api/steam/wishlist", cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<DealDto>> GetDealsAsync(CancellationToken cancellationToken) =>
        await SendAsync<List<DealDto>>(() => http.GetAsync("api/deals", cancellationToken), cancellationToken);

    public Task<DealDto> SearchDealAsync(string query, CancellationToken cancellationToken) =>
        SendAsync<DealDto>(() => http.GetAsync($"api/deals/search?q={Uri.EscapeDataString(query)}", cancellationToken), cancellationToken);

    public Task<DealDto> TrackGameAsync(string title, CancellationToken cancellationToken) =>
        SendAsync<DealDto>(() => http.PostAsJsonAsync("api/deals/tracked", new TrackRequest(title), cancellationToken), cancellationToken);

    public async Task UntrackGameAsync(int id, CancellationToken cancellationToken) =>
        await SendAsync(() => http.DeleteAsync($"api/deals/tracked/{id}", cancellationToken), cancellationToken);

    public Task<RefreshStatusDto> RefreshDealsAsync(CancellationToken cancellationToken) =>
        SendAsync<RefreshStatusDto>(() => http.PostAsync("api/deals/refresh", null, cancellationToken), cancellationToken);

    public Task<RefreshStatusDto> GetRefreshStatusAsync(CancellationToken cancellationToken) =>
        SendAsync<RefreshStatusDto>(() => http.GetAsync("api/deals/refresh", cancellationToken), cancellationToken);

    public Task<PromotionPageDto> GetPromotionsAsync(PromotionQuery query, CancellationToken cancellationToken) =>
        SendAsync<PromotionPageDto>(() => http.GetAsync($"api/promotions{PromotionQueryString(query)}", cancellationToken), cancellationToken);

    public Task<RefreshStatusDto> RefreshStoresAsync(CancellationToken cancellationToken) =>
        SendAsync<RefreshStatusDto>(() => http.PostAsync("api/stores/refresh", null, cancellationToken), cancellationToken);

    public Task<RefreshStatusDto> RefreshPromotionsAsync(CancellationToken cancellationToken) =>
        SendAsync<RefreshStatusDto>(() => http.PostAsync("api/promotions/refresh", null, cancellationToken), cancellationToken);

    public Task<CatalogPageDto> GetCatalogAsync(CatalogQuery query, bool refresh, CancellationToken cancellationToken)
    {
        List<string> parts =
        [
            $"store={Uri.EscapeDataString(query.Store ?? string.Empty)}",
            $"onlySale={query.OnlySale}",
            $"hideOwned={query.HideOwned}",
            $"hideExtras={query.HideExtras}",
            $"refresh={refresh}",
            $"skip={query.Skip}",
            $"take={query.Take}",
        ];
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"search={Uri.EscapeDataString(query.Search.Trim())}");
        }

        return SendAsync<CatalogPageDto>(() => http.GetAsync($"api/catalog?{string.Join('&', parts)}", cancellationToken), cancellationToken);
    }

    public Task<BundlePageDto> GetBundlesAsync(string store, bool refresh, CancellationToken cancellationToken) =>
        SendAsync<BundlePageDto>(() => http.GetAsync($"api/bundles?store={Uri.EscapeDataString(store)}&refresh={refresh}", cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<CartItemDto>> GetCartAsync(CancellationToken cancellationToken) =>
        await SendAsync<List<CartItemDto>>(() => http.GetAsync("api/cart", cancellationToken), cancellationToken);

    public Task<CartItemDto> SaveCartItemAsync(CartItemRequest request, CancellationToken cancellationToken) =>
        SendAsync<CartItemDto>(() => http.PostAsJsonAsync("api/cart", request, cancellationToken), cancellationToken);

    public async Task DeleteCartItemAsync(int id, CancellationToken cancellationToken) =>
        await SendAsync(() => http.DeleteAsync($"api/cart/{id}", cancellationToken), cancellationToken);

    private static string PromotionQueryString(PromotionQuery query)
    {
        List<string> parts =
        [
            $"onlyWishlist={query.OnlyWishlist}",
            $"hideOwned={query.HideOwned}",
            $"hideExtras={query.HideExtras}",
            $"multiStore={query.MultiStore}",
            $"skip={query.Skip}",
            $"take={query.Take}",
        ];
        if (!string.IsNullOrWhiteSpace(query.Store))
        {
            parts.Add($"store={Uri.EscapeDataString(query.Store)}");
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"search={Uri.EscapeDataString(query.Search.Trim())}");
        }

        if (query.MinDiscount is { } minDiscount)
        {
            parts.Add($"minDiscount={minDiscount}");
        }

        if (query.MaxPrice is { } maxPrice)
        {
            parts.Add($"maxPrice={maxPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (!string.IsNullOrWhiteSpace(query.Sort))
        {
            parts.Add($"sort={Uri.EscapeDataString(query.Sort)}");
        }

        return "?" + string.Join('&', parts);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static async Task<T> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendCoreAsync(send, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new ApiException("O servidor devolveu uma resposta vazia.");
    }

    private static async Task SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendCoreAsync(send, cancellationToken);
    }

    private static async Task<HttpResponseMessage> SendCoreAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException exception)
        {
            throw new ApiException("Não foi possível contactar o servidor.", exception);
        }

        if (!response.IsSuccessStatusCode)
        {
            string message = await ReadProblemMessageAsync(response, cancellationToken);
            response.Dispose();
            throw new ApiException(message);
        }

        return response;
    }

    private static async Task<string> ReadProblemMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string fallback = $"Erro do servidor ({(int)response.StatusCode}).";
        try
        {
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Object)
            {
                IEnumerable<string> messages = errors.EnumerateObject()
                    .SelectMany(property => property.Value.EnumerateArray())
                    .Select(item => item.GetString() ?? string.Empty);
                return string.Join(" ", messages);
            }

            if (root.TryGetProperty("detail", out JsonElement detail) && detail.GetString() is { Length: > 0 } detailText)
            {
                return detailText;
            }

            return root.TryGetProperty("title", out JsonElement title) && title.GetString() is { Length: > 0 } titleText
                ? titleText
                : fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }
}
