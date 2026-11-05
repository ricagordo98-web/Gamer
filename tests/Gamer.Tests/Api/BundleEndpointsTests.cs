using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Deals.Bundles;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Deals;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gamer.Tests.Api;

public sealed class BundleEndpointsTests : IClassFixture<GamerApiFactory>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BundleEndpointsTests(GamerApiFactory factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEnumerable<IBundleSource>>();
            services.AddSingleton<IEnumerable<IBundleSource>>([new FakeSource()]);
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
    public async Task Bundles_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync($"/api/bundles?store={ScraperCatalog.Fanatical}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Bundles_StoreWithoutBundles_ReturnsNotFound()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.GetAsync($"/api/bundles?store={ScraperCatalog.Steam}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Bundles_LoadsInBackground_AndReturnsBundlesWithItems()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        BundlePageDto? page = null;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            page = await client.GetFromJsonAsync<BundlePageDto>($"/api/bundles?store={ScraperCatalog.Fanatical}");
            if (page is { Loading: false, UpdatedAt: not null })
            {
                break;
            }

            await Task.Delay(50);
        }

        BundleDto bundle = page!.Items.Should().ContainSingle().Subject;
        bundle.Store.Should().Be(ScraperCatalog.Fanatical);
        bundle.Tiers.Should().Equal(new BundleTierDto(9.99m, 3));
        bundle.Items.Select(i => i.Title).Should().Equal("Hades", "Celeste", "Hollow Knight");
    }

    private sealed class FakeSource : IBundleSource
    {
        public string StoreName => ScraperCatalog.Fanatical;

        public Task<IReadOnlyList<StoreBundle>> ListAsync(Store store, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoreBundle>>(
            [
                new StoreBundle("Killer Bundle", "https://f.test/killer", null, null, null, "EUR", BundleKinds.Pick,
                    [new BundleTierDto(9.99m, 3)],
                    [new StoreBundleItem("Hades", null, null, 0), new StoreBundleItem("Celeste", null, null, 0), new StoreBundleItem("Hollow Knight", null, null, 0)]),
            ]);
    }
}
