using FluentAssertions;
using Gamer.Contracts.Games;
using Gamer.Ui.Games;

namespace Gamer.Tests.Ui;

public class DashboardStatsTests
{
    private static GameDto Game(string platform, GameStatus status, bool onDeck = false) =>
        new(1, "Game", 1, platform, "PC", PlatformFormat.Digital, status, onDeck, null, null);

    [Fact]
    public void From_MixedGames_CountsEachBucket()
    {
        DashboardStats stats = DashboardStats.From(
        [
            Game("Steam", GameStatus.Completed, onDeck: true),
            Game("Steam", GameStatus.Playing),
            Game("Epic", GameStatus.Backlog),
            Game("Steam", GameStatus.Backlog),
        ]);

        stats.Total.Should().Be(4);
        stats.Completed.Should().Be(1);
        stats.Playing.Should().Be(1);
        stats.Backlog.Should().Be(2);
        stats.OnDeck.Should().Be(1);
        stats.CompletionPercent.Should().Be(25);
        stats.ByPlatform.First().Should().Be(new PlatformCount("Steam", "PC", 3));
    }

    [Fact]
    public void From_NoGames_HasZeroCompletion()
    {
        DashboardStats.From([]).CompletionPercent.Should().Be(0);
    }
}
