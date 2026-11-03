using FluentAssertions;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Deals.Parsers;
using NSubstitute;

namespace Gamer.Tests.Api;

public class FetchingScraperTests
{
    private static readonly Store Shop = new() { Name = "Shop", BaseUrl = "https://shop.test/", Region = "PT", Currency = "EUR" };

    private static IPageFetcher Returning(string content)
    {
        IPageFetcher fetcher = Substitute.For<IPageFetcher>();
        fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(content);
        return fetcher;
    }

    private static IPageFetcher Failing(Exception exception)
    {
        IPageFetcher fetcher = Substitute.For<IPageFetcher>();
        fetcher.FetchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<string>(_ => throw exception);
        return fetcher;
    }

    private static IOfferParser ParserReturning(params StoreOffer[] offers)
    {
        IOfferParser parser = Substitute.For<IOfferParser>();
        parser.Parse(Arg.Any<string>(), Arg.Any<Store>()).Returns(offers);
        return parser;
    }

    private static FetchingScraper Create(IOfferParser parser, params IPageFetcher[] fetchers) =>
        new("Shop", (store, query) => $"{store.BaseUrl}?q={query}", parser, fetchers);

    private static StoreOffer Offer(string title) => new("Shop", title, 10m, 20m, "https://shop.test/x", "EUR");

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
    public async Task SearchAsync_OnlyOtherGamesReturned_IsUnavailable()
    {
        FetchingScraper scraper = Create(ParserReturning(Offer("Hades II")), Returning("a"));

        ScrapeResult result = await scraper.SearchAsync(Shop, "Hades", CancellationToken.None);

        result.IsAvailable.Should().BeFalse();
        result.FailureReason.Should().Be("No matching result.");
    }
}
