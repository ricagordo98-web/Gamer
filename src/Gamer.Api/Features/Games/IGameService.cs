using Gamer.Contracts.Games;

namespace Gamer.Api.Features.Games;

public interface IGameService
{
    Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<GameDto> CreateAsync(GameRequest request, CancellationToken cancellationToken);
    Task<GameDto?> UpdateAsync(int id, GameRequest request, CancellationToken cancellationToken);
    Task<GameDto?> ChangeStatusAsync(int id, GameStatus status, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
