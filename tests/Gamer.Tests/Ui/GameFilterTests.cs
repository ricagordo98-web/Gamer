using FluentAssertions;
using Gamer.Contracts.Games;
using Gamer.Ui.Games;

namespace Gamer.Tests.Ui;

public class GameFilterTests
{
    private static GameDto Game(int id, string title, int platformId = 1, GameStatus status = GameStatus.Backlog, bool onDeck = false) =>
        new(id, title, platformId, "PS2", "PlayStation", PlatformFormat.Physical, status, onDeck, null, null);

    private readonly List<GameDto> _games =
    [
        Game(1, "Hades", 12, GameStatus.Playing, onDeck: true),
        Game(2, "Ace Combat", 2, GameStatus.Completed),
        Game(3, "Alone in the Dark", 2),
    ];

    [Fact]
    public void Apply_SearchTerm_MatchesCaseInsensitively()
    {
        GameFilter filter = new() { Search = " ace " };

        filter.Apply(_games).Select(g => g.Id).Should().Equal(2);
    }

    [Fact]
    public void Apply_StatusAndPlatform_CombinesFilters()
    {
        GameFilter filter = new() { Status = GameStatus.Backlog, PlatformId = 2 };

        filter.Apply(_games).Select(g => g.Id).Should().Equal(3);
    }

    [Fact]
    public void Apply_OnlyDeck_ReturnsDeckGames()
    {
        GameFilter filter = new() { OnlyDeck = true };

        filter.Apply(_games).Select(g => g.Id).Should().Equal(1);
    }

    [Fact]
    public void ApplyExceptStatus_IgnoresStatus()
    {
        GameFilter filter = new() { Status = GameStatus.Completed };

        filter.ApplyExceptStatus(_games).Should().HaveCount(3);
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        GameFilter filter = new() { Search = "x", Status = GameStatus.Playing, PlatformId = 1, OnlyDeck = true };

        filter.Reset();

        filter.IsActive.Should().BeFalse();
    }
}
