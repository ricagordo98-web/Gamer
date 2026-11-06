using Gamer.Contracts.Games;

namespace Gamer.Ui.Games;

public enum GameSort
{
    Title,
    Platform,
    Status,
}

public class GameFilter
{
    private string? _family;

    public string Search { get; set; } = string.Empty;
    public GameStatus? Status { get; set; }
    public int? PlatformId { get; set; }
    public bool OnlyDeck { get; set; }
    public GameSort Sort { get; set; } = GameSort.Title;

    public string? Family
    {
        get => _family;
        set
        {
            if (_family != value)
            {
                PlatformId = null;
            }

            _family = value;
        }
    }

    public bool IsActive => Search.Length > 0 || Status is not null || PlatformId is not null || Family is not null || OnlyDeck;

    public IEnumerable<GameDto> Apply(IEnumerable<GameDto> games) =>
        Sorted(ApplyExceptStatus(games).Where(g => Status is null || g.Status == Status));

    public IEnumerable<GameDto> ApplyExceptStatus(IEnumerable<GameDto> games) =>
        ApplyExceptPlatform(games).Where(g => PlatformId is null || g.PlatformId == PlatformId);

    // Everything except the platform choice, used to show how many games each platform chip would give.
    public IEnumerable<GameDto> ApplyExceptPlatform(IEnumerable<GameDto> games)
    {
        string term = Search.Trim();
        return games.Where(g =>
            (term.Length == 0 || g.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            && (Family is null || g.PlatformFamily == Family)
            && (!OnlyDeck || g.OnDeck));
    }

    public void Reset()
    {
        Search = string.Empty;
        Status = null;
        PlatformId = null;
        _family = null;
        OnlyDeck = false;
    }

    private IEnumerable<GameDto> Sorted(IEnumerable<GameDto> games) => Sort switch
    {
        GameSort.Platform => games.OrderBy(g => g.PlatformFamily).ThenBy(g => g.PlatformName).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
        GameSort.Status => games.OrderBy(g => g.Status == GameStatus.Playing ? 0 : g.Status == GameStatus.Backlog ? 1 : 2).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
        _ => games.OrderBy(g => g.Title, StringComparer.OrdinalIgnoreCase),
    };
}
