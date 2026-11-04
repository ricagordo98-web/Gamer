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

    [Theory]
    [InlineData(20, "agora mesmo")]
    [InlineData(5 * 60, "há 5 min")]
    [InlineData(3 * 3600, "há 3 h")]
    [InlineData(2 * 86400, "há 2 d")]
    public void Ago_DescribesElapsedTime(int seconds, string expected)
    {
        DateTimeOffset now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        DealFormat.Ago(now.AddSeconds(-seconds), now).Should().Be(expected);
    }
}
