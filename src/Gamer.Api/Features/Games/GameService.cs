using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Games;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Games;

public class GameService(GamerDbContext db, TimeProvider clock) : IGameService
{
    public async Task<IReadOnlyList<GameDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.Games
            .AsNoTracking()
            .OrderBy(g => g.Title)
            .Select(GameMapping.ToDto)
            .ToListAsync(cancellationToken);

    public async Task<GameDto> CreateAsync(GameRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        Game game = new()
        {
            Title = request.Title.Trim(),
            PlatformId = request.PlatformId,
            Status = request.Status,
            OnDeck = request.OnDeck,
            Rating = request.Rating,
            Notes = request.Notes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(game.Id, cancellationToken);
    }

    public async Task<GameDto?> UpdateAsync(int id, GameRequest request, CancellationToken cancellationToken)
    {
        Game? game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            return null;
        }

        game.Title = request.Title.Trim();
        game.PlatformId = request.PlatformId;
        game.Status = request.Status;
        game.OnDeck = request.OnDeck;
        game.Rating = request.Rating;
        game.Notes = request.Notes;
        game.UpdatedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<GameDto?> ChangeStatusAsync(int id, GameStatus status, CancellationToken cancellationToken)
    {
        Game? game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            return null;
        }

        game.Status = status;
        game.UpdatedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        int deleted = await db.Games.Where(g => g.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    private async Task<GameDto> GetAsync(int id, CancellationToken cancellationToken) =>
        await db.Games
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(GameMapping.ToDto)
            .SingleAsync(cancellationToken);
}
