using FluentAssertions;
using Gamer.Api.Features.Deals;
using Gamer.Contracts.Deals;

namespace Gamer.Tests.Api;

public class DealBuilderTests
{
    private static StoreOffer Offer(string store, decimal price, decimal original = 30m, string currency = "EUR") =>
        new(store, "Hades", price, original, $"https://{store}.test/hades", currency);

    [Fact]
    public void Build_PicksCheapestAndComputesSaving()
    {
        List<StoreOffer> offers = [Offer("A", 20m), Offer("B", 12m), Offer("C", 15m)];

        DealDto deal = DealBuilder.Build("Hades", null, null, false, false, offers, []);

        deal.Best!.Store.Should().Be("B");
        deal.Saving.Should().Be(3m);
        deal.Offers.Select(o => o.Store).Should().Equal("B", "C", "A");
    }

    [Fact]
    public void Build_SingleOffer_HasNoSaving()
    {
        DealDto deal = DealBuilder.Build("Hades", null, null, false, false, [Offer("A", 20m)], []);

        deal.Best!.Store.Should().Be("A");
        deal.Saving.Should().BeNull();
    }

    [Fact]
    public void Build_NoOffers_HasNoBest()
    {
        DealDto deal = DealBuilder.Build("Hades", null, null, false, false, [], [new StoreStatusDto("A", false, "blocked")]);

        deal.Best.Should().BeNull();
        deal.Stores.Should().ContainSingle();
    }

    [Fact]
    public void Build_MixedCurrencies_ComparesDominantCurrencyOnly()
    {
        List<StoreOffer> offers = [Offer("A", 20m), Offer("B", 18m), Offer("C", 5m, currency: "USD")];

        DealDto deal = DealBuilder.Build("Hades", null, null, false, false, offers, []);

        deal.Best!.Store.Should().Be("B");
    }

    [Fact]
    public void Offer_Discount_IsRoundedPercentage() =>
        Offer("A", 15m, 30m).DiscountPercent.Should().Be(50);
}
