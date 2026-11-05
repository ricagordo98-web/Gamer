using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Deals;
using Microsoft.Extensions.DependencyInjection;

namespace Gamer.Tests.Api;

public sealed class CartEndpointsTests(GamerApiFactory factory) : IClassFixture<GamerApiFactory>
{
    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "secret"));
        LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private static CartItemRequest Game(string url, decimal price = 10m, DateTimeOffset? endsAt = null) =>
        new(CartKinds.Game, ScraperCatalog.Fanatical, "Hades", url, null, price, 25m, "EUR", endsAt, null);

    private static async Task<CartItemDto> SaveAsync(HttpClient client, CartItemRequest request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/cart", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CartItemDto>())!;
    }

    [Fact]
    public async Task Cart_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/cart");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Save_SameUrlTwice_UpdatesInsteadOfDuplicating()
    {
        HttpClient client = await CreateAuthorizedClientAsync();
        const string url = "https://f.test/upsert";

        CartItemDto first = await SaveAsync(client, Game(url, 10m));
        CartItemDto second = await SaveAsync(client, Game(url, 8m));
        List<CartItemDto>? all = await client.GetFromJsonAsync<List<CartItemDto>>("/api/cart");

        second.Id.Should().Be(first.Id);
        all!.Where(i => i.Url == url).Should().ContainSingle().Which.Price.Should().Be(8m);
    }

    [Fact]
    public async Task Save_BundleWithChosenGames_KeepsTheGames()
    {
        HttpClient client = await CreateAuthorizedClientAsync();
        CartItemRequest bundle = new(CartKinds.Bundle, ScraperCatalog.Fanatical, "Killer Bundle", "https://f.test/killer", null, 9.99m, 40m, "EUR", null,
            [new CartGameDto("Hades", "https://img/h.png"), new CartGameDto("Celeste", null)]);

        CartItemDto saved = await SaveAsync(client, bundle);

        saved.Kind.Should().Be(CartKinds.Bundle);
        saved.Games.Select(g => g.Title).Should().Equal("Hades", "Celeste");
    }

    [Fact]
    public async Task Save_InvalidRequest_ReturnsValidationProblem()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/cart", Game("not-a-url") with { Kind = "car" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_RemovesTheItem()
    {
        HttpClient client = await CreateAuthorizedClientAsync();
        CartItemDto saved = await SaveAsync(client, Game("https://f.test/delete"));

        HttpResponseMessage deleted = await client.DeleteAsync($"/api/cart/{saved.Id}");
        HttpResponseMessage again = await client.DeleteAsync($"/api/cart/{saved.Id}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        again.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Status_ReflectsEndDateAndCurrentPrice()
    {
        HttpClient client = await CreateAuthorizedClientAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await SaveAsync(client, Game("https://f.test/ended", endsAt: now.AddHours(-1)));
        await SaveAsync(client, Game("https://f.test/soon", endsAt: now.AddHours(5)));
        await SaveAsync(client, Game("https://f.test/later", endsAt: now.AddDays(10)));
        await SaveAsync(client, Game("https://f.test/up", 10m));
        await SaveAsync(client, Game("https://f.test/down", 10m));
        await AddPromotionAsync("https://f.test/up", 20m);
        await AddPromotionAsync("https://f.test/down", 6m);

        Dictionary<string, CartItemDto> items = (await client.GetFromJsonAsync<List<CartItemDto>>("/api/cart"))!.ToDictionary(i => i.Url);

        items["https://f.test/ended"].Status.Should().Be(CartStatuses.Ended);
        items["https://f.test/soon"].Status.Should().Be(CartStatuses.EndingSoon);
        items["https://f.test/later"].Status.Should().Be(CartStatuses.Active);
        items["https://f.test/up"].Status.Should().Be(CartStatuses.PriceUp);
        items["https://f.test/up"].CurrentPrice.Should().Be(20m);
        items["https://f.test/down"].Status.Should().Be(CartStatuses.PriceDown);
    }

    private async Task AddPromotionAsync(string url, decimal price)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        GamerDbContext db = scope.ServiceProvider.GetRequiredService<GamerDbContext>();
        Store store = db.Stores.Single(s => s.Name == ScraperCatalog.Fanatical);
        db.Promotions.Add(new Promotion
        {
            StoreId = store.Id,
            Title = "Hades",
            TitleKey = "hades",
            Price = price,
            OriginalPrice = 25m,
            Url = url,
            Currency = "EUR",
            CapturedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
