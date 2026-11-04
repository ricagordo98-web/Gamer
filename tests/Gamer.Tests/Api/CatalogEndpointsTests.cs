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

public sealed class CatalogEndpointsTests : IClassFixture<GamerApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CatalogEndpointsTests(GamerApiFactory factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEnumerable<IStoreScraper>>();
            services.AddSingleton<IEnumerable<IStoreScraper>>([new CatalogScraper(ScraperCatalog.Steam), new CatalogScraper(ScraperCatalog.Fanatical)]);
        }));

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "secret"));
        LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private static async Task<CatalogPageDto> LoadedAsync(HttpClient client, string query)
    {
        CatalogPageDto? page = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            page = await client.GetFromJsonAsync<CatalogPageDto>($"/api/catalog{query}");
            if (page is { Loading: false, UpdatedAt: not null })
            {
                break;
            }

            await Task.Delay(50);
        }

        return page!;
    }

    [Fact]
    public async Task Catalog_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/api/catalog?store=Steam");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Catalog_UnknownStore_ReturnsNotFound()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/catalog?store=Nope");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Catalog_PutsDiscountedGamesFirst_ThenTheRest()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        CatalogPageDto page = await LoadedAsync(client, "?store=Steam");

        page.Items.Select(i => i.Title).Should().Equal("Hollow Knight", "Hades", "Celeste", "Stardew Valley");
        page.OnSale.Should().Be(2);
        page.Total.Should().Be(4);
    }

    [Fact]
    public async Task Catalog_OnlySale_HidesFullPriceGames()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        CatalogPageDto page = await LoadedAsync(client, "?store=Steam&onlySale=true");

        page.Items.Select(i => i.Title).Should().Equal("Hollow Knight", "Hades");
    }

    [Fact]
    public async Task Catalog_MergesDealsAndCatalogWithoutDuplicates_AndHidesExtras()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        CatalogPageDto page = await LoadedAsync(client, "?store=Steam&hideExtras=false&search=hades");

        page.Items.Select(i => i.Title).Should().Equal("Hades", "Hades Soundtrack DLC");
    }

    [Fact]
    public async Task Catalog_PagingSlicesTheResult()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        CatalogPageDto page = await LoadedAsync(client, "?store=Steam&skip=1&take=2");

        page.Items.Should().HaveCount(2);
        page.Total.Should().Be(4);
    }

    private sealed class CatalogScraper(string storeName) : IStoreScraper
    {
        public string StoreName => storeName;

        public Task<ScrapeResult> SearchAsync(Store store, string title, CancellationToken cancellationToken) =>
            Task.FromResult(ScrapeResult.Unavailable(StoreStatusReasons.NotFound));

        public Task<ScrapeResult> ListDealsAsync(Store store, CancellationToken cancellationToken) =>
            Task.FromResult(ScrapeResult.Ok([Offer("Hades", 12.49m, 24.99m), Offer("Hollow Knight", 3m, 15m)]));

        public Task<ScrapeResult> ListCatalogAsync(Store store, int maxItems, CancellationToken cancellationToken) =>
            Task.FromResult(ScrapeResult.Ok(
            [
                Offer("Celeste", 19.99m, 19.99m),
                Offer("Hades", 12.49m, 24.99m),
                Offer("Stardew Valley", 14.99m, 14.99m),
                Offer("Hades Soundtrack DLC", 9.99m, 9.99m),
            ]));

        private StoreOffer Offer(string title, decimal price, decimal original) =>
            new(storeName, title, price, original, $"https://{storeName}.test/{title}", "EUR");
    }
}
