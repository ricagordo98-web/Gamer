using FluentAssertions;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Deals.Parsers;

namespace Gamer.Tests.Api;

public sealed class PromotionEndParsingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("15 Days Left", 15 * 24 * 60)]
    [InlineData("1 Day Left", 24 * 60)]
    [InlineData("Ends in 5 hours", 5 * 60)]
    [InlineData("2d 3h 10m", (2 * 24 + 3) * 60 + 10)]
    [InlineData("Ends in 03:12:00", 3 * 60 + 12)]
    public void CountdownParser_ReadsRelativeTimes(string text, int minutes)
    {
        DateTimeOffset? end = CountdownParser.Parse(text, Now);

        end.Should().Be(Now.AddMinutes(minutes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Add To Cart")]
    public void CountdownParser_WithoutTime_ReturnsNull(string? text)
    {
        CountdownParser.Parse(text, Now).Should().BeNull();
    }

    [Fact]
    public void SteamSpecialsParser_ReadsDiscountExpiration()
    {
        const string json = """
            {"specials":{"items":[
              {"id":10,"name":"Cheap One","discounted":true,"original_price":2000,"final_price":1000,"currency":"EUR","discount_expiration":1794312000}]}}
            """;
        Store store = new() { Name = "Steam", BaseUrl = "https://store.steampowered.com/", Region = "PT", Currency = "EUR" };

        StoreOffer offer = new SteamSpecialsParser().Parse(json, store).Single();

        offer.EndsAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1794312000));
    }

    [Fact]
    public void HtmlOfferParser_ReadsCountdownWhenSelectorIsSet()
    {
        const string html = """
            <div class="card"><h3>Hades</h3><span class="price">10,00 €</span><a href="/hades"></a><div class="timer">7 Days Left</div></div>
            <div class="card"><h3>Celeste</h3><span class="price">5,00 €</span><a href="/celeste"></a></div>
            """;
        HtmlOfferParser parser = new(new HtmlSelectors(".card", "h3", ".price", ".was", "a", "img", Countdown: ".timer"));
        Store store = new() { Name = "Shop", BaseUrl = "https://shop.test/", Region = "PT", Currency = "EUR" };

        IReadOnlyList<StoreOffer> offers = parser.Parse(html, store);

        offers[0].EndsAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
        offers[1].EndsAt.Should().BeNull();
    }
}
