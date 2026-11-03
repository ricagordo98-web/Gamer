using Gamer.Contracts.Games;

namespace Gamer.Ui.Games;

public static class GameStatusExtensions
{
    public static string Label(this GameStatus status) => status switch
    {
        GameStatus.Backlog => "Backlog",
        GameStatus.Playing => "A jogar",
        GameStatus.Completed => "Terminado",
        _ => status.ToString(),
    };

    public static GameStatus Next(this GameStatus status) => status switch
    {
        GameStatus.Backlog => GameStatus.Playing,
        GameStatus.Playing => GameStatus.Completed,
        _ => GameStatus.Backlog,
    };

    public static string CssClass(this GameStatus status) => status.ToString().ToLowerInvariant();
}
