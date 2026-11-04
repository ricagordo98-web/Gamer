using System.Text;
using FluentAssertions;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Deals.Parsers;

namespace Gamer.Tests.Api;

public class StoreParsersTests
{
    private static Store CreateStore(string name, string baseUrl) =>
        new() { Name = name, BaseUrl = baseUrl, Region = "PT", Currency = "EUR" };

    [Fact]
    public void SteamParser_ReadsPricesInCents_AndSkipsItemsWithoutPrice()
    {
        const string json = """
            {"total":2,"items":[
              {"type":"app","name":"Hades","id":1145360,"price":{"currency":"EUR","initial":2499,"final":1249}},
              {"type":"app","name":"Hades Soundtrack","id":999}
            ]}
            """;

        IReadOnlyList<StoreOffer> offers = new SteamSearchParser().Parse(json, CreateStore("Steam", "https://store.steampowered.com/"));

        StoreOffer offer = offers.Should().ContainSingle().Subject;
        offer.Price.Should().Be(12.49m);
        offer.OriginalPrice.Should().Be(24.99m);
        offer.DiscountPercent.Should().Be(50);
        offer.Url.Should().Be("https://store.steampowered.com/app/1145360");
        offer.ImageUrl.Should().Be("https://cdn.akamai.steamstatic.com/steam/apps/1145360/header.jpg");
    }

    [Fact]
    public void SteamParser_InvalidJson_ThrowsParseException()
    {
        Action act = () => new SteamSearchParser().Parse("<html>nope</html>", CreateStore("Steam", "https://store.steampowered.com/"));

        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void SteamSpecialsParser_ReadsDiscountedItemsFromBothSections_WithoutDuplicates()
    {
        const string json = """
            {"specials":{"items":[
              {"id":10,"name":"Cheap One","discounted":true,"discount_percent":50,"original_price":2000,"final_price":1000,"currency":"EUR"},
              {"id":11,"name":"Full Price","discounted":false,"original_price":2000,"final_price":2000,"currency":"EUR"}]},
             "top_sellers":{"items":[
              {"id":10,"name":"Cheap One","discounted":true,"discount_percent":50,"original_price":2000,"final_price":1000,"currency":"EUR"},
              {"id":12,"name":"Popular","discounted":true,"discount_percent":25,"original_price":4000,"final_price":3000,"currency":"EUR"}]}}
            """;

        IReadOnlyList<StoreOffer> offers = new SteamSpecialsParser().Parse(json, CreateStore("Steam", "https://store.steampowered.com/"));

        offers.Select(o => o.Title).Should().BeEquivalentTo("Cheap One", "Popular");
        offers.Single(o => o.Title == "Popular").DiscountPercent.Should().Be(25);
    }

    [Fact]
    public void SteamSpecialsParser_NoSections_ThrowsParseException()
    {
        Action act = () => new SteamSpecialsParser().Parse("{}", CreateStore("Steam", "https://store.steampowered.com/"));

        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void HtmlParser_ReadsItemsWithSelectors_AndResolvesRelativeLinks()
    {
        const string html = """
            <div class="game"><a class="n" href="/games/hades">Hades</a><img src="/img/hades.jpg"><span class="was">24,99 €</span><span class="now">12,49 €</span></div>
            <div class="game"><a class="n" href="/games/other">Broken item</a></div>
            """;
        HtmlOfferParser parser = new(new HtmlSelectors(".game", ".n", ".now", ".was", ".n", "img"));

        IReadOnlyList<StoreOffer> offers = parser.Parse(html, CreateStore("Shop", "https://shop.test/"));

        StoreOffer offer = offers.Should().ContainSingle().Subject;
        offer.Title.Should().Be("Hades");
        offer.Price.Should().Be(12.49m);
        offer.OriginalPrice.Should().Be(24.99m);
        offer.Url.Should().Be("https://shop.test/games/hades");
        offer.ImageUrl.Should().Be("https://shop.test/img/hades.jpg");
    }

    [Fact]
    public void HtmlParser_NoItems_ThrowsNoResultsException()
    {
        HtmlOfferParser parser = new(new HtmlSelectors(".game", ".n", ".now", ".was", ".n", "img"));

        Action act = () => parser.Parse("<div id=\"app\"></div>", CreateStore("Shop", "https://shop.test/"));

        act.Should().Throw<NoResultsException>();
    }

    [Fact]
    public void HtmlParser_NoOriginalPrice_DerivesItFromDiscountBadge()
    {
        const string html = """
            <div class="item"><a class="cover" href="/g/1"><img data-src="https://cdn.test/1.jpg" src="/lazy.svg"><div class="discount"> -50% </div></a><div class="title"> Game </div><div class="price">10.00&nbsp;€</div></div>
            """;
        HtmlOfferParser parser = new(new HtmlSelectors(".item", ".title", ".price", ".retail-price", "a.cover", "a.cover img", ".discount"));

        StoreOffer offer = parser.Parse(html, CreateStore("Instant Gaming", "https://ig.test/")).Single();

        offer.Price.Should().Be(10m);
        offer.OriginalPrice.Should().Be(20m);
        offer.DiscountPercent.Should().Be(50);
        offer.ImageUrl.Should().Be("https://cdn.test/1.jpg");
    }

    [Fact]
    public void HtmlParser_NameOnlyInImageAlt_UsesAltAndNormalizer()
    {
        const string html = """
            <div class="card"><img alt="Product cover for Avowed" src="https://cdn.test/a.jpg"><a class="buy" href="/en/game/avowed"><span class="was">€39.99</span><span class="now">€19.99</span></a></div>
            """;
        HtmlOfferParser parser = new(
            new HtmlSelectors(".card", "img[alt]", ".now", ".was", "a.buy", "img"),
            name => name.Replace("Product cover for ", string.Empty, StringComparison.Ordinal));

        StoreOffer offer = parser.Parse(html, CreateStore("Fanatical", "https://fan.test/")).Single();

        offer.Title.Should().Be("Avowed");
        offer.Price.Should().Be(19.99m);
        offer.OriginalPrice.Should().Be(39.99m);
        offer.DiscountPercent.Should().Be(50);
    }

    [Fact]
    public void GreenManGamingParser_DecodesEmbeddedPayload()
    {
        const string json = """
            {"selectedEdition":{"sku":"Castlevania - PC","url":"/games/castlevania-pc/","imageUrl":"https://img.test/c.jpg","rrp":"29,99€","currentPrice":"22,49€","currentPriceInDecimal":22.49,"discountPercentOff":25},"displayName":"Castlevania: Belmont's Curse"}
            """;
        string payload = Convert.ToBase64String(Encoding.Unicode.GetBytes(json));
        string html = $"""<ul><li class="ais-Hits-item"><div ng-init="init('{payload}', 1)"></div></li><li class="ais-Hits-item"><div ng-init="init('!!', 1)"></div></li></ul>""";

        IReadOnlyList<StoreOffer> offers = new GreenManGamingParser().Parse(html, CreateStore("Green Man Gaming", "https://www.greenmangaming.com/"));

        StoreOffer offer = offers.Should().ContainSingle().Subject;
        offer.Title.Should().Be("Castlevania: Belmont's Curse");
        offer.Price.Should().Be(22.49m);
        offer.OriginalPrice.Should().Be(29.99m);
        offer.DiscountPercent.Should().Be(25);
        offer.Url.Should().Be("https://www.greenmangaming.com/games/castlevania-pc/");
        offer.ImageUrl.Should().Be("https://img.test/c.jpg");
    }

    [Fact]
    public void GreenManGamingParser_NoItems_ThrowsNoResultsException()
    {
        Action act = () => new GreenManGamingParser().Parse("<html></html>", CreateStore("Green Man Gaming", "https://www.greenmangaming.com/"));

        act.Should().Throw<NoResultsException>();
    }

    [Fact]
    public void ScraperCatalog_ParsesGamesplanetOfferMarkup()
    {
        const string html = """
            <div class="game_list col-8"><div class="card"><a class="responsive-img" href="/game/re-requiem--7680-1"><img alt="Resident Evil Requiem" src="https://gpstatic.com/a.jpg"></a>
            <div class="card-body"><h4 class="d-none"><a href="/game/re-requiem--7680-1">Resident Evil Requiem</a></h4>
            <span class="prices"><span class="price_base"><strike>69,99€</strike></span> <span class="price_saving false">-37%</span> <span class="price_current">43,99€</span></span></div></div></div>
            """;
        Store store = CreateStore(ScraperCatalog.Gamesplanet, "https://de.gamesplanet.com/");
        IPageFetcher fetcher = new StaticFetcher(html);
        IStoreScraper scraper = ScraperCatalog.Create(fetcher, fetcher).Single(s => s.StoreName == ScraperCatalog.Gamesplanet);

        ScrapeResult result = scraper.ListDealsAsync(store, CancellationToken.None).GetAwaiter().GetResult();

        StoreOffer offer = result.Offers.Should().ContainSingle().Subject;
        offer.Title.Should().Be("Resident Evil Requiem");
        offer.Price.Should().Be(43.99m);
        offer.OriginalPrice.Should().Be(69.99m);
        offer.Url.Should().Be("https://de.gamesplanet.com/game/re-requiem--7680-1");
        offer.ImageUrl.Should().Be("https://gpstatic.com/a.jpg");
    }

    [Fact]
    public void ScraperCatalog_ParsesHumbleMarkup()
    {
        const string html = """
            <ul><li class="entity-block-container"><div class="entity on-sale"><a class="entity-link" href="/store/silent-hill-2"><div class="entity-details"><img class="entity-image" src="https://hb.imgix.net/s.jpg"><span class="entity-title">SILENT HILL 2</span></div></a>
            <div class="entity-pricing"><div class="price-container"><div class="discount-amount"> -60%<span> OFF</span></div><span class="breakdown-full-price">€69.99</span><div class="price-button"><button class="price price-inner-button">€27.99</button></div></div></div></div></li></ul>
            """;
        Store store = CreateStore(ScraperCatalog.HumbleBundle, "https://www.humblebundle.com/");
        IPageFetcher fetcher = new StaticFetcher(html);
        IStoreScraper scraper = ScraperCatalog.Create(fetcher, fetcher).Single(s => s.StoreName == ScraperCatalog.HumbleBundle);

        StoreOffer offer = scraper.ListDealsAsync(store, CancellationToken.None).GetAwaiter().GetResult().Offers.Single();

        offer.Title.Should().Be("SILENT HILL 2");
        offer.Price.Should().Be(27.99m);
        offer.OriginalPrice.Should().Be(69.99m);
        offer.Url.Should().Be("https://www.humblebundle.com/store/silent-hill-2");
    }

    private sealed class StaticFetcher(string content) : IPageFetcher
    {
        public Task<string> FetchAsync(string url, string? waitForSelector, CancellationToken cancellationToken) => Task.FromResult(content);
    }
}
