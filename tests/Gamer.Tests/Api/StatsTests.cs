using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Stats;
using Gamer.Api.Features.Steam;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Stats;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Gamer.Tests.Api;

public sealed class StatsTests : IClassFixture<GamerApiFactory>
{
    private const string DetailsJson = """
        {"620":{"success":true,"data":{"is_free":false,"price_overview":{"currency":"EUR","initial":999,"final":249,"discount_percent":75},
        "metacritic":{"score":95},"genres":[{"id":"1","description":"Action"},{"id":"2","description":"Puzzle"}],"release_date":{"coming_soon":false,"date":"9 Apr, 2011"}}}}
        """;

    private readonly WebApplicationFactory<Program> _factory;

    public StatsTests(GamerApiFactory factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISteamStoreClient>();
            services.AddSingleton(Substitute.For<ISteamStoreClient>());
            services.AddSingleton(sp => sp.GetRequiredService<ISteamStoreClient>());
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
    public async Task Stats_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/api/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void ParseDetails_PaidGame_ReadsPriceMetacriticAndGenres()
    {
        using JsonDocument document = JsonDocument.Parse(DetailsJson);

        SteamStoreDetails? details = SteamStoreClient.ParseDetails(document.RootElement, 620);

        details.Should().NotBeNull();
        details!.Price.Should().Be(2.49m);
        details.NormalPrice.Should().Be(9.99m);
        details.DiscountPercent.Should().Be(75);
        details.Metacritic.Should().Be(95);
        details.Genres.Should().Equal("Action", "Puzzle");
        details.ReleaseDate.Should().Be("9 Apr, 2011");
    }

    [Fact]
    public void ParseDetails_FreeGame_HasZeroPrice()
    {
        using JsonDocument document = JsonDocument.Parse("""{"10":{"success":true,"data":{"is_free":true}}}""");

        SteamStoreDetails? details = SteamStoreClient.ParseDetails(document.RootElement, 10);

        details!.Price.Should().Be(0m);
    }

    [Fact]
    public void ParseDetails_UnsuccessfulResponse_ReturnsNull()
    {
        using JsonDocument document = JsonDocument.Parse("""{"10":{"success":false}}""");

        SteamStoreClient.ParseDetails(document.RootElement, 10).Should().BeNull();
    }

    [Fact]
    public void ParseReviews_ComputesPercentage()
    {
        using JsonDocument document = JsonDocument.Parse("""{"success":1,"query_summary":{"review_score_desc":"Overwhelmingly Positive","total_positive":970,"total_negative":30}}""");

        SteamReviews? reviews = SteamStoreClient.ParseReviews(document.RootElement);

        reviews.Should().Be(new SteamReviews(97, 1000, "Overwhelmingly Positive"));
    }

    [Fact]
    public void ParseSpy_ReadsOwnersAverageAndTopTags()
    {
        using JsonDocument document = JsonDocument.Parse("""{"appid":620,"owners":"10,000,000 .. 20,000,000","average_forever":420,"tags":{"Puzzle":900,"Co-op":1500,"Funny":300}}""");

        SteamSpyData? spy = SteamStoreClient.ParseSpy(document.RootElement);

        spy!.Owners.Should().Be("10,000,000 – 20,000,000");
        spy.AverageMinutes.Should().Be(420);
        spy.Tags.Should().Equal("Co-op", "Puzzle", "Funny");
    }

    [Fact]
    public void ParseSpy_WithVotes_ProvidesReviewFallback()
    {
        using JsonDocument document = JsonDocument.Parse("""{"appid":1,"owners":"1 .. 2","average_forever":0,"positive":90,"negative":10,"tags":[]}""");

        SteamSpyData? spy = SteamStoreClient.ParseSpy(document.RootElement);

        spy!.Reviews.Should().Be(new SteamReviews(90, 100, null));
        spy.AverageMinutes.Should().BeNull();
    }

    [Fact]
    public void ParseHistoricLow_ReadsPriceAndShop()
    {
        using JsonDocument document = JsonDocument.Parse("""[{"id":"abc","low":{"shop":{"id":61,"name":"Steam"},"price":{"amount":1.99,"currency":"EUR"},"cut":80}}]""");

        HistoricLowClient.Parse(document.RootElement).Should().Be(new HistoricLow(1.99m, "Steam"));
    }

    [Fact]
    public async Task Enrich_StoresInfoForLibraryGamesAndStopsAfterRepeatedFailures()
    {
        using TestDatabase database = new();
        using (GamerDbContext seed = database.CreateContext())
        {
            seed.SteamLibrary.AddRange(Enumerable.Range(1, 8).Select(i => new SteamLibraryItem { AppId = i, Title = $"Game {i}", PlaytimeMinutes = 100 - i }));
            await seed.SaveChangesAsync();
        }

        ISteamStoreClient store = Substitute.For<ISteamStoreClient>();
        store.GetInfoAsync(1, Arg.Any<CancellationToken>()).Returns(new SteamStoreInfo(
            new SteamStoreDetails(5m, 10m, "EUR", 50, 80, ["RPG"], null), new SteamReviews(90, 100, "Very Positive"), new SteamSpyData("1 – 2", 300, ["Open World"])));
        store.GetInfoAsync(Arg.Is<long>(id => id > 1), Arg.Any<CancellationToken>()).Returns<Task<SteamStoreInfo>>(_ => throw new SteamException("blocked"));
        ISteamSettingsService settings = Substitute.For<ISteamSettingsService>();
        settings.GetItadKeyAsync(Arg.Any<CancellationToken>()).Returns((string?)null);

        using GamerDbContext db = database.CreateContext();
        SteamEnrichmentService service = new(db, store, Substitute.For<IHistoricLowClient>(), settings, TimeProvider.System, NullLogger<SteamEnrichmentService>.Instance);

        await service.RunAsync(TimeSpan.Zero, (_, _) => { }, CancellationToken.None);

        SteamAppInfo saved = db.SteamAppInfos.Single();
        saved.AppId.Should().Be(1);
        saved.ReviewPercent.Should().Be(90);
        saved.Genres.Should().Be("RPG");
        saved.Tags.Should().Be("Open World");
        await store.Received(5).GetInfoAsync(Arg.Is<long>(id => id > 1), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stats_AggregatesPlaytimeValueAndNeverPlayed()
    {
        using TestDatabase database = new();
        using GamerDbContext db = database.CreateContext();
        db.SteamLibrary.AddRange(
            new SteamLibraryItem { AppId = 1, Title = "Played", PlaytimeMinutes = 120, RecentMinutes = 30 },
            new SteamLibraryItem { AppId = 2, Title = "Unopened", PlaytimeMinutes = 0 });
        db.SteamAppInfos.AddRange(
            new SteamAppInfo { AppId = 1, Price = 5m, NormalPrice = 20m, Currency = "EUR", Genres = "RPG, Action" },
            new SteamAppInfo { AppId = 2, Price = 10m, NormalPrice = 10m, Currency = "EUR", Genres = "Action" });
        await db.SaveChangesAsync();

        StatsDto stats = await new StatsService(db, new SteamEnrichmentRunner(Substitute.For<IServiceScopeFactory>(), NullLogger<SteamEnrichmentRunner>.Instance), TimeProvider.System).GetAsync(CancellationToken.None);

        stats.SteamGames.Should().Be(2);
        stats.NeverPlayed.Should().Be(1);
        stats.TotalMinutes.Should().Be(120);
        stats.ValueNormal.Should().Be(30m);
        stats.ValueCurrent.Should().Be(15m);
        stats.Currency.Should().Be("EUR");
        stats.Recent.Select(g => g.Title).Should().Equal("Played");
        stats.NeverPlayedTop.Select(g => g.Title).Should().Equal("Unopened");
        stats.Genres.First().Should().Be(new StatsCountDto("Action", 2, 120));
    }

    [Fact]
    public async Task Stats_BucketsRecencyTagsAndRankings()
    {
        using TestDatabase database = new();
        using GamerDbContext db = database.CreateContext();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        db.SteamLibrary.AddRange(
            new SteamLibraryItem { AppId = 1, Title = "Never", PlaytimeMinutes = 0 },
            new SteamLibraryItem { AppId = 2, Title = "Short", PlaytimeMinutes = 90, LastPlayedAt = now.AddDays(-2) },
            new SteamLibraryItem { AppId = 3, Title = "Mid", PlaytimeMinutes = 300, LastPlayedAt = now.AddDays(-20) },
            new SteamLibraryItem { AppId = 4, Title = "Long", PlaytimeMinutes = 4000, LastPlayedAt = now.AddDays(-800) });
        db.SteamAppInfos.AddRange(
            new SteamAppInfo { AppId = 1, Price = 40m, NormalPrice = 40m, Currency = "EUR", ReviewPercent = 80, ReviewCount = 50 },
            new SteamAppInfo { AppId = 2, Price = 9m, NormalPrice = 9m, Currency = "EUR", ReviewPercent = 95, ReviewCount = 1000, Tags = "Indie, Roguelike" },
            new SteamAppInfo { AppId = 3, Price = 30m, NormalPrice = 30m, Currency = "EUR", ReviewPercent = 90, ReviewCount = 2000, Tags = "Indie" },
            new SteamAppInfo { AppId = 4, Price = 20m, NormalPrice = 20m, Currency = "EUR", ReviewPercent = 70, ReviewCount = 5000 });
        await db.SaveChangesAsync();

        StatsDto stats = await new StatsService(db, new SteamEnrichmentRunner(Substitute.For<IServiceScopeFactory>(), NullLogger<SteamEnrichmentRunner>.Instance), TimeProvider.System).GetAsync(CancellationToken.None);

        stats.NeverPlayedValue.Should().Be(40m);
        stats.PlaytimeBuckets.Select(b => b.Count).Should().Equal(1, 1, 1, 0, 1);
        stats.Recency.Select(b => b.Count).Should().Equal(1, 1, 0, 0, 1);
        stats.Tags.First().Should().Be(new StatsCountDto("Indie", 2, 390));
        stats.BestRated.Select(g => g.Title).Should().Equal("Short", "Mid", "Long");
        stats.BestValue.Select(g => g.Title).Should().Equal("Long", "Short", "Mid");
    }

    [Fact]
    public async Task Games_ListsEveryLibraryGameWithItsInfo()
    {
        using TestDatabase database = new();
        using GamerDbContext db = database.CreateContext();
        db.SteamLibrary.AddRange(
            new SteamLibraryItem { AppId = 1, Title = "Beta", PlaytimeMinutes = 10 },
            new SteamLibraryItem { AppId = 2, Title = "Alpha", PlaytimeMinutes = 0 });
        db.SteamAppInfos.Add(new SteamAppInfo { AppId = 1, Price = 5m, DiscountPercent = 50, Metacritic = 88, Genres = "RPG" });
        await db.SaveChangesAsync();

        IReadOnlyList<StatsRowDto> rows = await new StatsService(db, new SteamEnrichmentRunner(Substitute.For<IServiceScopeFactory>(), NullLogger<SteamEnrichmentRunner>.Instance), TimeProvider.System).GetGamesAsync(CancellationToken.None);

        rows.Select(r => r.Title).Should().Equal("Alpha", "Beta");
        rows[1].Should().Match<StatsRowDto>(r => r.Price == 5m && r.DiscountPercent == 50 && r.Metacritic == 88 && r.Genres == "RPG");
        rows[0].Price.Should().BeNull();
    }

    [Fact]
    public async Task GetApp_UnknownGame_ReturnsNotFound()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.GetAsync("/api/stats/apps/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetApp_AtHistoricLow_FlagsIt()
    {
        using TestDatabase database = new();
        using GamerDbContext db = database.CreateContext();
        db.SteamLibrary.Add(new SteamLibraryItem { AppId = 5, Title = "Cheap", PlaytimeMinutes = 10 });
        db.SteamAppInfos.Add(new SteamAppInfo { AppId = 5, Price = 2m, NormalPrice = 10m, LowestPrice = 2m, Genres = "Indie, RPG" });
        await db.SaveChangesAsync();

        SteamAppDetailDto? detail = await new StatsService(db, new SteamEnrichmentRunner(Substitute.For<IServiceScopeFactory>(), NullLogger<SteamEnrichmentRunner>.Instance), TimeProvider.System).GetAppAsync(5, CancellationToken.None);

        detail!.AtHistoricLow.Should().BeTrue();
        detail.Genres.Should().Equal("Indie", "RPG");
    }

    [Fact]
    public async Task Players_ReturnsCountsFromTheStore()
    {
        ISteamStoreClient store = Substitute.For<ISteamStoreClient>();
        store.GetCurrentPlayersAsync(620, Arg.Any<CancellationToken>()).Returns(1234);
        PlayerCountCache cache = new(store, TimeProvider.System);

        IReadOnlyList<PlayersNowDto> players = await cache.GetManyAsync([620, 620], CancellationToken.None);
        await cache.GetAsync(620, CancellationToken.None);

        players.Should().ContainSingle().Which.Should().Be(new PlayersNowDto(620, 1234));
        await store.Received(1).GetCurrentPlayersAsync(620, Arg.Any<CancellationToken>());
    }
}
