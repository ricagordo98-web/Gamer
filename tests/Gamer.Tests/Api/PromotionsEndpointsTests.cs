using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Deals;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gamer.Tests.Api;

public sealed class PromotionsEndpointsTests : IClassFixture<GamerApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PromotionsEndpointsTests(GamerApiFactory factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEnumerable<IStoreScraper>>();
            services.AddSingleton<IEnumerable<IStoreScraper>>(
            [
                new ListingScraper(ScraperCatalog.Steam, Deal("Hades", 12.49m, 24.99m), Deal("Hades Soundtrack DLC", 2m, 9.99m), Deal("Celeste", 4.99m, 19.99m)),
                new ListingScraper(ScraperCatalog.HumbleBundle, Deal("Hades", 10.99m, 24.99m), Deal("Hollow Knight", 3m, 15m)),
                new ListingScraper(ScraperCatalog.Fanatical),
            ]);
        }));

    private static (string Title, decimal Price, decimal Original) Deal(string title, decimal price, decimal original) => (title, price, original);

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "secret"));
        LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private async Task<HttpClient> RefreshedClientAsync()
    {
        HttpClient client = await CreateAuthorizedClientAsync();
        await client.PostAsync("/api/promotions/refresh", null);
        for (int attempt = 0; attempt < 100; attempt++)
        {
            RefreshStatusDto? status = await client.GetFromJsonAsync<RefreshStatusDto>("/api/deals/refresh");
            if (status is { Running: false, PromotionsCompletedAt: not null })
            {
                break;
            }

            await Task.Delay(50);
        }

        return client;
    }

    private static Task<PromotionPageDto?> GetAsync(HttpClient client, string query) =>
        client.GetFromJsonAsync<PromotionPageDto>($"/api/promotions{query}");

    [Fact]
    public async Task Promotions_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/api/promotions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Promotions_GroupsTheSameGameAcrossStores_AndPicksCheapestAsBest()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, string.Empty);

        PromotionDto hades = page!.Items.Single(i => i.Title == "Hades");
        hades.Best.Store.Should().Be(ScraperCatalog.HumbleBundle);
        hades.Best.Price.Should().Be(10.99m);
        hades.Saving.Should().Be(14m);
        hades.ImageUrl.Should().BeNull();
        hades.Others.Should().ContainSingle().Which.Store.Should().Be(ScraperCatalog.Steam);
    }

    [Fact]
    public async Task Promotions_DefaultHidesExtras_AndCanShowThem()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? hidden = await GetAsync(client, string.Empty);
        PromotionPageDto? shown = await GetAsync(client, "?hideExtras=false");

        hidden!.Items.Should().NotContain(i => i.Title.Contains("Soundtrack"));
        shown!.Items.Should().Contain(i => i.Title.Contains("Soundtrack") && i.IsExtra);
    }

    [Fact]
    public async Task Promotions_StoreFilter_ShowsThatStoresOfferAsBest()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, "?store=Steam");

        page!.Items.Should().OnlyContain(i => i.Best.Store == ScraperCatalog.Steam);
        page.Items.Single(i => i.Title == "Hades").Best.Price.Should().Be(12.49m);
        page.Items.Should().NotContain(i => i.Title == "Hollow Knight");
    }

    [Fact]
    public async Task Promotions_SeveralStores_ShowsCheapestOfferAmongThem()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, $"?store={ScraperCatalog.Steam},{Uri.EscapeDataString(ScraperCatalog.HumbleBundle)}");

        page!.Items.Select(i => i.Title).Should().BeEquivalentTo("Hades", "Celeste", "Hollow Knight");
        page.Items.Single(i => i.Title == "Hades").Best.Store.Should().Be(ScraperCatalog.HumbleBundle);
    }

    [Fact]
    public async Task Promotions_MinDiscountAndMaxPrice_FilterOnTheBestOffer()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? discounted = await GetAsync(client, "?minDiscount=75");
        PromotionPageDto? cheap = await GetAsync(client, "?maxPrice=4");

        discounted!.Items.Select(i => i.Title).Should().BeEquivalentTo("Celeste", "Hollow Knight");
        cheap!.Items.Select(i => i.Title).Should().BeEquivalentTo("Hollow Knight");
    }

    [Fact]
    public async Task Promotions_MultiStore_KeepsOnlyGamesSoldInSeveralStores()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, "?multiStore=true");

        page!.Items.Should().ContainSingle().Which.Title.Should().Be("Hades");
    }

    [Fact]
    public async Task Promotions_SortByName_OrdersAlphabetically()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, "?sort=name");

        page!.Items.Select(i => i.Title).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Promotions_SortByDiscount_PutsBiggestDiscountFirst()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, "?sort=discount");

        page!.Items.Select(i => i.Best.DiscountPercent).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Promotions_SearchFilter_MatchesAllWordsInTheTitle()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, "?search=hollow%20knight");

        page!.Items.Should().ContainSingle().Which.Title.Should().Be("Hollow Knight");
    }

    [Fact]
    public async Task Promotions_Paging_ReturnsTotalAndSlices()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, "?take=1&skip=1&sort=name");

        page!.Items.Should().HaveCount(1);
        page.Total.Should().Be(3);
    }

    [Fact]
    public async Task Promotions_ReportsPerStoreCountsAndFailures()
    {
        HttpClient client = await RefreshedClientAsync();

        PromotionPageDto? page = await GetAsync(client, string.Empty);

        page!.Stores.Single(s => s.Store == ScraperCatalog.Steam).Count.Should().Be(3);
        PromotionStoreDto fanatical = page.Stores.Single(s => s.Store == ScraperCatalog.Fanatical);
        fanatical.Count.Should().Be(0);
        fanatical.Error.Should().Be("blocked");
        page.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Promotions_RefreshAgain_ReplacesInsteadOfDuplicating()
    {
        HttpClient client = await RefreshedClientAsync();
        await client.PostAsync("/api/promotions/refresh", null);
        await Task.Delay(300);

        PromotionPageDto? page = await GetAsync(client, "?hideExtras=false");

        page!.Total.Should().Be(4);
    }

    private sealed class ListingScraper(string storeName, params (string Title, decimal Price, decimal Original)[] deals) : IStoreScraper
    {
        public string StoreName => storeName;

        public Task<ScrapeResult> SearchAsync(Store store, string title, CancellationToken cancellationToken) =>
            Task.FromResult(ScrapeResult.Unavailable(Contracts.Deals.StoreStatusReasons.NotFound));

        public Task<ScrapeResult> ListDealsAsync(Store store, CancellationToken cancellationToken) =>
            Task.FromResult(deals.Length == 0
                ? ScrapeResult.Unavailable("blocked")
                : ScrapeResult.Ok(deals.Select(d => new StoreOffer(storeName, d.Title, d.Price, d.Original, $"https://{storeName}.test/{d.Title}", "EUR")).ToList()));

        public Task<ScrapeResult> ListCatalogAsync(Store store, int maxItems, Action<IReadOnlyList<StoreOffer>>? onPage, CancellationToken cancellationToken) =>
            Task.FromResult(ScrapeResult.Unavailable("no catalog"));
    }
}
