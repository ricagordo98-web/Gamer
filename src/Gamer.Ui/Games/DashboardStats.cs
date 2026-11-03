using Gamer.Contracts.Games;

namespace Gamer.Ui.Games;

public record PlatformCount(string Name, string Family, int Count);

public record DashboardStats(
    int Total,
    int Backlog,
    int Playing,
    int Completed,
    int OnDeck,
    IReadOnlyList<PlatformCount> ByPlatform)
{
    public int CompletionPercent => Total == 0 ? 0 : (int)Math.Round(100.0 * Completed / Total);

    public static DashboardStats From(IReadOnlyCollection<GameDto> games, int topPlatforms = 8) => new(
        games.Count,
        games.Count(g => g.Status == GameStatus.Backlog),
        games.Count(g => g.Status == GameStatus.Playing),
        games.Count(g => g.Status == GameStatus.Completed),
        games.Count(g => g.OnDeck),
        games.GroupBy(g => new { g.PlatformName, g.PlatformFamily })
            .Select(group => new PlatformCount(group.Key.PlatformName, group.Key.PlatformFamily, group.Count()))
            .OrderByDescending(p => p.Count)
            .ThenBy(p => p.Name)
            .Take(topPlatforms)
            .ToList());
}
