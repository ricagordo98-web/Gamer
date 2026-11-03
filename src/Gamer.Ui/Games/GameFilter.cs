using Gamer.Contracts.Games;

namespace Gamer.Ui.Games;

public class GameFilter
{
    public string Search { get; set; } = string.Empty;
    public GameStatus? Status { get; set; }
    public int? PlatformId { get; set; }
    public bool OnlyDeck { get; set; }

    public bool IsActive => Search.Length > 0 || Status is not null || PlatformId is not null || OnlyDeck;

    public IEnumerable<GameDto> Apply(IEnumerable<GameDto> games) =>
        ApplyExceptStatus(games).Where(g => Status is null || g.Status == Status);

    public IEnumerable<GameDto> ApplyExceptStatus(IEnumerable<GameDto> games)
    {
        string term = Search.Trim();
        return games.Where(g =>
            (term.Length == 0 || g.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            && (PlatformId is null || g.PlatformId == PlatformId)
            && (!OnlyDeck || g.OnDeck));
    }

    public void Reset()
    {
        Search = string.Empty;
        Status = null;
        PlatformId = null;
        OnlyDeck = false;
    }
}
