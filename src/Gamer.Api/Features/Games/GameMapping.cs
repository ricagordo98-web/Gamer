using System.Linq.Expressions;
using Gamer.Api.Domain;
using Gamer.Contracts.Games;

namespace Gamer.Api.Features.Games;

public static class GameMapping
{
    public static readonly Expression<Func<Game, GameDto>> ToDto = game => new GameDto(
        game.Id,
        game.Title,
        game.PlatformId,
        game.Platform.Name,
        game.Platform.Family,
        game.Platform.Format,
        game.Status,
        game.OnDeck,
        game.Rating,
        game.Notes);
}
