using FluentAssertions;
using Gamer.Api.Features.Import;
using Gamer.Contracts.Games;

namespace Gamer.Tests.Api;

public class PlatformParserTests
{
    [Theory]
    [InlineData("L - PS2", "PS2", "PlayStation", PlatformFormat.Physical)]
    [InlineData("L - GAMECUBE", "GameCube", "Nintendo", PlatformFormat.Physical)]
    [InlineData("PC - STEAM", "Steam", "PC", PlatformFormat.Digital)]
    [InlineData("PC - Cracked", "Cracked", "PC", PlatformFormat.Digital)]
    [InlineData("PS4", "PS4", "PlayStation", PlatformFormat.Digital)]
    public void Parse_KnownPlatform_ReturnsCatalogData(string raw, string name, string family, PlatformFormat format)
    {
        ParsedPlatform result = PlatformParser.Parse(raw);

        result.Should().Be(new ParsedPlatform(name, family, format));
    }

    [Fact]
    public void Parse_UnknownPcPlatform_UsesPcFamily()
    {
        ParsedPlatform result = PlatformParser.Parse("PC - Itch");

        result.Should().Be(new ParsedPlatform("Itch", "PC", PlatformFormat.Digital));
    }

    [Fact]
    public void Parse_UnknownPhysicalPlatform_UsesOtherFamily()
    {
        ParsedPlatform result = PlatformParser.Parse("L - Dreamcast");

        result.Should().Be(new ParsedPlatform("Dreamcast", "Other", PlatformFormat.Physical));
    }
}
