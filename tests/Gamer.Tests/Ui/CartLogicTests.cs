using FluentAssertions;
using Gamer.Contracts.Deals;
using Gamer.Ui.Games;

namespace Gamer.Tests.Ui;

public sealed class CartLogicTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<BundleTierDto> Tiers = [new(9.99m, 3), new(14.99m, 5), new(19.95m, 7)];

    private static CartItemDto Item(string status, DateTimeOffset? endsAt = null, decimal? current = null) =>
        new(7, CartKinds.Game, "Fanatical", "Hades", "https://f.test/h", null, 10m, 25m, "EUR", current, endsAt, Now, status, []);

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 3)]
    [InlineData(4, 5)]
    [InlineData(6, 7)]
    [InlineData(7, 7)]
    public void PackFor_PicksTheSmallestPackThatFitsTheChosenGames(int picked, int expectedCount)
    {
        BundlePricing.PackFor(Tiers, picked).Count.Should().Be(expectedCount);
    }

    [Fact]
    public void MaxPicks_IsTheLargestPack()
    {
        BundlePricing.MaxPicks(Tiers).Should().Be(7);
    }

    [Fact]
    public void For_ActiveItem_HasNoAlert()
    {
        CartAlerts.For(Item(CartStatuses.Active), Now).Should().BeNull();
    }

    [Fact]
    public void For_EndingSoon_UsesADifferentKeyInTheLastHours()
    {
        CartAlert? twoDays = CartAlerts.For(Item(CartStatuses.EndingSoon, Now.AddHours(30)), Now);
        CartAlert? lastHours = CartAlerts.For(Item(CartStatuses.EndingSoon, Now.AddHours(3)), Now);

        twoDays!.Key.Should().NotBe(lastHours!.Key);
        lastHours.Message.Should().Contain("acaba em 3 h");
    }

    [Fact]
    public void For_PriceDown_IsGoodNews()
    {
        CartAlert? alert = CartAlerts.For(Item(CartStatuses.PriceDown, current: 6m), Now);

        alert!.Good.Should().BeTrue();
        alert.Message.Should().Contain("10,00 € → 6,00 €");
    }
}
