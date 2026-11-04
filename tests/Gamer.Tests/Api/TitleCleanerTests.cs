using FluentAssertions;
using Gamer.Api.Features.Deals;

namespace Gamer.Tests.Api;

public class TitleCleanerTests
{
    [Theory]
    [InlineData("Hades - PC & Mac (Steam)", "Hades")]
    [InlineData("Hades II - PC & XBOX Series X|S (Microsoft Store)", "Hades II")]
    [InlineData("Football Manager 27 - PC & Mac (Steam) - Europe", "Football Manager 27")]
    [InlineData("Minecraft: Java & Bedrock Edition - PC", "Minecraft: Java & Bedrock Edition")]
    [InlineData("Air Hares - PC", "Air Hares")]
    [InlineData("Celeste PC", "Celeste")]
    [InlineData("Portal 2 Steam Key", "Portal 2")]
    [InlineData("Elden Ring", "Elden Ring")]
    [InlineData("Dead Cells (GOG)", "Dead Cells")]
    [InlineData("  Spaced   Out  ", "Spaced   Out")]
    public void Clean_RemovesPlatformAndStoreNoise(string raw, string expected) =>
        TitleCleaner.Clean(raw).Should().Be(expected);

    [Fact]
    public void Clean_Null_ReturnsEmpty() => TitleCleaner.Clean(null).Should().BeEmpty();

    [Fact]
    public void Clean_DecodesHtmlEntities() => TitleCleaner.Clean("Ratchet &amp; Clank - PC").Should().Be("Ratchet & Clank");

    [Theory]
    [InlineData("Hades Soundtrack DLC", true)]
    [InlineData("Stellar End Demo Version", true)]
    [InlineData("Roblox - 1000 Robux", true)]
    [InlineData("Valorant 20 EUR - 2050 Riot Points - Europe", true)]
    [InlineData("Resident Evil 7 - Season Pass", true)]
    [InlineData("Hades", false)]
    [InlineData("Elden Ring", false)]
    public void IsExtra_RecognizesNonGameContent(string title, bool expected) =>
        ExtraContent.IsExtra(title).Should().Be(expected);
}
