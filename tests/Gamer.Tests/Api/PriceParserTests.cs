using FluentAssertions;
using Gamer.Api.Features.Deals;

namespace Gamer.Tests.Api;

public class PriceParserTests
{
    [Theory]
    [InlineData("12,49€", 12.49)]
    [InlineData("€12.49", 12.49)]
    [InlineData("1.299,00 €", 1299.00)]
    [InlineData("1,299.00", 1299.00)]
    [InlineData("$9.99", 9.99)]
    [InlineData("7,99 €", 7.99)]
    [InlineData("15", 15)]
    public void TryParse_ValidText_ReturnsAmount(string text, double expected)
    {
        PriceParser.TryParse(text, out decimal amount).Should().BeTrue();
        amount.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Free")]
    [InlineData(null)]
    public void TryParse_NoNumber_ReturnsFalse(string? text) =>
        PriceParser.TryParse(text, out _).Should().BeFalse();
}
