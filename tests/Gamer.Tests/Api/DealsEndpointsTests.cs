using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Steam;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Deals;
using Gamer.Contracts.Steam;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gamer.Tests.Api;

public sealed class DealsEndpointsTests : IClassFixture<GamerApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DealsEndpointsTests(GamerApiFactory factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEnumerable<IStoreScraper>>();
            services.AddSingleton<IEnumerable<IStoreScraper>>(
            [
                new FakeScraper(ScraperCatalog.Steam, 12.49m),
                new FakeScraper(ScraperCatalog.HumbleBundle, 10.99m),
                new FakeScraper(ScraperCatalog.Fanatical, null),
            ]);
            services.RemoveAll<ISteamClient>();
            services.AddSingleton<ISteamClient, FakeSteamClient>();
        }));

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "secret"));
        LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    [Fact]
    public async Task GetStores_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/api/stores");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStores_ReturnsSeededStores()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        List<StoreDto>? stores = await client.GetFromJsonAsync<List<StoreDto>>("/api/stores");

        stores!.Select(s => s.Name).Should().Contain(["Steam", "Humble Bundle", "Fanatical", "Gamesplanet", "Green Man Gaming", "Instant Gaming"]);
    }

    [Fact]
    public async Task UpdateStore_InvalidUrl_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/stores/1", new StoreUpdateRequest(true, "not a url"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateStore_AddsTrailingSlash()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/stores/6", new StoreUpdateRequest(true, "https://www.instant-gaming.com"));
        StoreDto? store = await response.Content.ReadFromJsonAsync<StoreDto>();

        store!.BaseUrl.Should().Be("https://www.instant-gaming.com/");
    }

    [Fact]
    public async Task SteamSettings_InvalidSteamId_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/steam/settings", new SteamSettingsRequest("key", "123", 12));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SteamSync_WithoutSettings_ReturnsBadRequest()
    {
        using GamerApiFactory isolated = new();
        HttpClient client = isolated.CreateClient();
        HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "secret"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token);

        HttpResponseMessage response = await client.PostAsync("/api/steam/sync", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SteamSettings_SaveAndSync_ExposesLibraryAndWishlist_WithoutLeakingApiKey()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage save = await client.PutAsJsonAsync("/api/steam/settings", new SteamSettingsRequest("secret-key", "76561198000000000", 6));
        string body = await save.Content.ReadAsStringAsync();
        HttpResponseMessage sync = await client.PostAsync("/api/steam/sync", null);
        SteamSyncResultDto? result = await sync.Content.ReadFromJsonAsync<SteamSyncResultDto>();
        List<SteamLibraryItemDto>? library = await client.GetFromJsonAsync<List<SteamLibraryItemDto>>("/api/steam/library");
        List<WishlistItemDto>? wishlist = await client.GetFromJsonAsync<List<WishlistItemDto>>("/api/steam/wishlist");

        body.Should().NotContain("secret-key").And.Contain("\"apiKeyConfigured\":true");
        result.Should().Be(new SteamSyncResultDto(1, 1, 1));
        library!.Single().Title.Should().Be("Owned Game");
        wishlist!.Single().Title.Should().Be("Hades");
    }

    [Fact]
    public async Task Search_ReturnsBestOfferAndStoreStatuses()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        DealDto? deal = await client.GetFromJsonAsync<DealDto>("/api/deals/search?q=Hades");

        deal!.Best!.Store.Should().Be(ScraperCatalog.HumbleBundle);
        deal.Saving.Should().Be(1.50m);
        deal.Stores.Should().Contain(s => s.Store == ScraperCatalog.Fanatical && !s.Available);
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsBadRequest()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/deals/search?q=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Track_PersistsPricesAndAppearsInDeals_ThenCanBeRemoved()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage track = await client.PostAsJsonAsync("/api/deals/tracked", new TrackRequest("Celeste"));
        DealDto? tracked = await track.Content.ReadFromJsonAsync<DealDto>();
        List<DealDto>? deals = await client.GetFromJsonAsync<List<DealDto>>("/api/deals");
        HttpResponseMessage delete = await client.DeleteAsync($"/api/deals/tracked/{tracked!.TrackedId}");

        tracked.Best!.Store.Should().Be(ScraperCatalog.HumbleBundle);
        deals!.Should().Contain(d => d.Title == "Celeste" && d.Best != null);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private sealed class FakeScraper(string storeName, decimal? price) : IStoreScraper
    {
        public string StoreName => storeName;

        public Task<ScrapeResult> SearchAsync(Store store, string title, CancellationToken cancellationToken) =>
            Task.FromResult(price is null
                ? ScrapeResult.Unavailable("blocked")
                : ScrapeResult.Ok([new StoreOffer(storeName, title, price.Value, 24.99m, $"https://{storeName}.test/", "EUR")]));

        public Task<ScrapeResult> ListDealsAsync(Store store, CancellationToken cancellationToken) =>
            Task.FromResult(price is null
                ? ScrapeResult.Unavailable("blocked")
                : ScrapeResult.Ok(
                [
                    new StoreOffer(storeName, "Hades", price.Value, 24.99m, $"https://{storeName}.test/hades", "EUR", "https://img.test/hades.jpg"),
                    new StoreOffer(storeName, "Hades Soundtrack DLC", 2m, 9.99m, $"https://{storeName}.test/ost", "EUR"),
                ]));

        public Task<ScrapeResult> ListCatalogAsync(Store store, int maxItems, Action<IReadOnlyList<StoreOffer>>? onPage, CancellationToken cancellationToken) =>
            Task.FromResult(ScrapeResult.Unavailable("no catalog"));
    }

    private sealed class FakeSteamClient : ISteamClient
    {
        public Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string apiKey, string steamId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SteamOwnedGame>>([new SteamOwnedGame(1, "Owned Game", 120)]);

        public Task<IReadOnlyList<SteamWishlistEntry>> GetWishlistAsync(string apiKey, string steamId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SteamWishlistEntry>>([new SteamWishlistEntry(1145360, null)]);

        public Task<IReadOnlyDictionary<long, string>> GetAppNamesAsync(IReadOnlyCollection<long> appIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<long, string>>(new Dictionary<long, string> { [1145360] = "Hades" });
    }
}
