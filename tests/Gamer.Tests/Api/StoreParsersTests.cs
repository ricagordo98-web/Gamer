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
    }

    [Fact]
    public void SteamParser_InvalidJson_ThrowsParseException()
    {
        Action act = () => new SteamSearchParser().Parse("<html>nope</html>", CreateStore("Steam", "https://store.steampowered.com/"));

        act.Should().Throw<ParseException>();
    }

    [Fact]
    public void HumbleParser_ReadsPriceTuples_AndUnwrapsBrowserPreBlock()
    {
        const string json = """
            <html><body><pre>{"results":[{"human_name":"Hades","machine_name":"hades_storefront","current_price":[12.49,"EUR"],"full_price":[24.99,"EUR"]}]}</pre></body></html>
            """;

        IReadOnlyList<StoreOffer> offers = new HumbleSearchParser().Parse(json, CreateStore("Humble Bundle", "https://www.humblebundle.com/"));

        StoreOffer offer = offers.Should().ContainSingle().Subject;
        offer.Price.Should().Be(12.49m);
        offer.Url.Should().Be("https://www.humblebundle.com/store/hades_storefront");
    }

    [Fact]
    public void HtmlParser_ReadsItemsWithSelectors_AndResolvesRelativeLinks()
    {
        const string html = """
            <div class="game"><a class="n" href="/games/hades">Hades</a><span class="was">24,99 €</span><span class="now">12,49 €</span></div>
            <div class="game"><a class="n" href="/games/other">Broken item</a></div>
            """;
        HtmlOfferParser parser = new(new HtmlSelectors(".game", ".n", ".now", ".was", ".n"));

        IReadOnlyList<StoreOffer> offers = parser.Parse(html, CreateStore("Shop", "https://shop.test/"));

        StoreOffer offer = offers.Should().ContainSingle().Subject;
        offer.Title.Should().Be("Hades");
        offer.Price.Should().Be(12.49m);
        offer.OriginalPrice.Should().Be(24.99m);
        offer.Url.Should().Be("https://shop.test/games/hades");
    }

    [Fact]
    public void HtmlParser_NoItems_ThrowsParseException()
    {
        HtmlOfferParser parser = new(new HtmlSelectors(".game", ".n", ".now", ".was", ".n"));

        Action act = () => parser.Parse("<div id=\"app\"></div>", CreateStore("Shop", "https://shop.test/"));

        act.Should().Throw<ParseException>();
    }
}
