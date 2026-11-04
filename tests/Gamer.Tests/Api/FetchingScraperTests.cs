using FluentAssertions;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Deals.Parsers;
using Gamer.Contracts.Deals;
using NSubstitute;

namespace Gamer.Tests.Api;

public class FetchingScraperTests
{
    private static readonly Store Shop = new() { Name = "Shop", BaseUrl = "https://shop.test/", Region = "PT", Currency = "EUR" };

    private static IPageFetcher Returning(string content)
    {
        IPageFetcher fetcher = Substitute.For<IPageFetcher>();
        fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(content);
        return fetcher;
    }

    private static IPageFetcher Failing(Exception exception)
    {
        IPageFetcher fetcher = Substitute.For<IPageFetcher>();
        fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns<string>(_ => throw exception);
        return fetcher;
    }

    private static IOfferParser ParserReturning(params StoreOffer[] offers)
    {
        IOfferParser parser = Substitute.For<IOfferParser>();
        parser.Parse(Arg.Any<string>(), Arg.Any<Store>()).Returns(offers);
        return parser;
    }

    private static FetchingScraper Create(IOfferParser parser, params IPageFetcher[] fetchers) =>
        new("Shop", new ScraperPages((store, query) => $"{store.BaseUrl}?q={query}", store => $"{store.BaseUrl}deals", parser, parser, ".item"), fetchers);

    private static StoreOffer Offer(string title, decimal price = 10m, decimal original = 20m) =>
        new("Shop", title, price, original, "https://shop.test/x", "EUR");

    [Fact]
    public async Task SearchAsync_FirstFetcherBlocked_FallsBackToSecond()
    {
        IPageFetcher blocked = Failing(new BlockedException("cloudflare"));
        IPageFetcher browser = Returning("<html/>");
        FetchingScraper scraper = Create(ParserReturning(Offer("Hades")), blocked, browser);

        ScrapeResult result = await scraper.SearchAsync(Shop, "Hades", CancellationToken.None);

        result.IsAvailable.Should().BeTrue();
        result.Offers.Should().ContainSingle();
    }

    [Fact]
    public async Task SearchAsync_AllFetchersFail_ReturnsUnavailableWithLastReason()
    {
        FetchingScraper scraper = Create(
            ParserReturning(),
            Failing(new BlockedException("cloudflare")),
            Failing(new BrowserUnavailableException("no chromium")));

        ScrapeResult result = await scraper.SearchAsync(Shop, "Hades", CancellationToken.None);

        result.IsAvailable.Should().BeFalse();
        result.FailureReason.Should().Be("no chromium");
    }

    [Fact]
    public async Task SearchAsync_ParserThrows_TriesNextFetcher()
    {
        IOfferParser parser = Substitute.For<IOfferParser>();
        parser.Parse(Arg.Any<string>(), Arg.Any<Store>())
            .Returns(_ => throw new ParseException("empty shell"), _ => [Offer("Hades")]);
        FetchingScraper scraper = Create(parser, Returning("a"), Returning("b"));

        ScrapeResult result = await scraper.SearchAsync(Shop, "Hades", CancellationToken.None);

        result.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_OnlyOtherGamesReturned_IsNotFound()
    {
        FetchingScraper scraper = Create(ParserReturning(Offer("Hades II")), Returning("a"));

        ScrapeResult result = await scraper.SearchAsync(Shop, "Hades", CancellationToken.None);

        result.IsAvailable.Should().BeFalse();
        result.FailureReason.Should().Be(StoreStatusReasons.NotFound);
    }

    [Fact]
    public async Task SearchAsync_PageWithoutItems_IsNotFoundInsteadOfFailure()
    {
        IOfferParser parser = Substitute.For<IOfferParser>();
        parser.Parse(Arg.Any<string>(), Arg.Any<Store>()).Returns(_ => throw new NoResultsException("empty"));
        FetchingScraper scraper = Create(parser, Returning("a"));

        ScrapeResult result = await scraper.SearchAsync(Shop, "Zzzz", CancellationToken.None);

        result.FailureReason.Should().Be(StoreStatusReasons.NotFound);
    }

    [Fact]
    public async Task SearchAsync_StoreTitleHasPlatformSuffix_StillMatchesAndIsCleaned()
    {
        FetchingScraper scraper = Create(ParserReturning(Offer("Hades - PC & Mac (Steam)")), Returning("a"));

        ScrapeResult result = await scraper.SearchAsync(Shop, "Hades", CancellationToken.None);

        result.Offers.Should().ContainSingle().Which.Title.Should().Be("Hades");
    }

    [Fact]
    public async Task ListDealsAsync_KeepsOnlyDiscountedOffers()
    {
        FetchingScraper scraper = Create(ParserReturning(Offer("Hades", 10m, 20m), Offer("Full price game", 20m, 20m)), Returning("a"));

        ScrapeResult result = await scraper.ListDealsAsync(Shop, CancellationToken.None);

        result.Offers.Should().ContainSingle().Which.Title.Should().Be("Hades");
    }

    [Fact]
    public async Task ListDealsAsync_NothingDiscounted_IsUnavailable()
    {
        FetchingScraper scraper = Create(ParserReturning(Offer("Full price game", 20m, 20m)), Returning("a"));

        ScrapeResult result = await scraper.ListDealsAsync(Shop, CancellationToken.None);

        result.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task ListDealsAsync_EmptyPage_ReportsLayoutProblemNotNotFound()
    {
        IOfferParser parser = Substitute.For<IOfferParser>();
        parser.Parse(Arg.Any<string>(), Arg.Any<Store>()).Returns(_ => throw new NoResultsException("empty"));
        FetchingScraper scraper = Create(parser, Returning("a"));

        ScrapeResult result = await scraper.ListDealsAsync(Shop, CancellationToken.None);

        result.IsAvailable.Should().BeFalse();
        result.FailureReason.Should().NotBe(StoreStatusReasons.NotFound);
    }

    [Fact]
    public async Task ListDealsAsync_RequestsTheListingUrlWithWaitSelector()
    {
        IPageFetcher fetcher = Returning("a");
        FetchingScraper scraper = Create(ParserReturning(Offer("Hades")), fetcher);

        await scraper.ListDealsAsync(Shop, CancellationToken.None);

        await fetcher.Received(1).FetchAsync("https://shop.test/deals", ".item", Arg.Any<CancellationToken>());
    }
}
