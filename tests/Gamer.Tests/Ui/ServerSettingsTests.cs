using FluentAssertions;
using Gamer.Ui.Services;

namespace Gamer.Tests.Ui;

public class ServerSettingsTests
{
    [Theory]
    [InlineData("192.168.1.10:5199", "http://192.168.1.10:5199/")]
    [InlineData("  http://localhost:5199/api ", "http://localhost:5199/")]
    [InlineData("https://gamer.example.com", "https://gamer.example.com/")]
    public void Normalize_ValidInput_ReturnsAuthorityWithTrailingSlash(string input, string expected)
    {
        ServerSettings.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://server")]
    public void Normalize_InvalidInput_ReturnsNull(string? input)
    {
        ServerSettings.Normalize(input).Should().BeNull();
    }
}
