using FluentAssertions;
using Gamer.Ui.Games;

namespace Gamer.Tests.Ui;

public class DealFormatTests
{
    [Theory]
    [InlineData(12.5, "EUR", "12,50 €")]
    [InlineData(9.99, "USD", "$9,99")]
    [InlineData(5, "SEK", "5,00 SEK")]
    public void Money_FormatsBySymbol(double amount, string currency, string expected) =>
        DealFormat.Money((decimal)amount, currency).Should().Be(expected);

    [Theory]
    [InlineData(45, "45 min")]
    [InlineData(150, "2 h")]
    public void Playtime_FormatsMinutesAndHours(int minutes, string expected) =>
        DealFormat.Playtime(minutes).Should().Be(expected);
}
