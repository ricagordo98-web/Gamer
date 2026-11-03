using FluentAssertions;
using Gamer.Api.Features.Deals;

namespace Gamer.Tests.Api;

public class TitleMatcherTests
{
    [Theory]
    [InlineData("The Witcher® 3: Wild Hunt", "the witcher 3 wild hunt")]
    [InlineData("DOOM™ Eternal – Deluxe Edition", "doom eternal")]
    [InlineData("  Hades   ", "hades")]
    public void Normalize_StripsNoise(string input, string expected) =>
        TitleMatcher.Normalize(input).Should().Be(expected);

    [Theory]
    [InlineData("Hades", "HADES")]
    [InlineData("The Witcher 3: Wild Hunt", "The Witcher 3: Wild Hunt - Game of the Year Edition")]
    [InlineData("Dark Souls III", "Dark Souls III Deluxe Edition")]
    public void IsMatch_SameGame_ReturnsTrue(string query, string candidate) =>
        TitleMatcher.IsMatch(query, candidate).Should().BeTrue();

    [Theory]
    [InlineData("Hades", "Hades II")]
    [InlineData("Dark Souls", "Dark Souls III")]
    [InlineData("Portal", "Portal 2")]
    [InlineData("Hades", "Hollow Knight")]
    public void IsMatch_DifferentGame_ReturnsFalse(string query, string candidate) =>
        TitleMatcher.IsMatch(query, candidate).Should().BeFalse();
}
