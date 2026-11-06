using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Games;
using Gamer.Contracts.Steam;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Steam;

public class SteamSyncService(GamerDbContext db, ISteamClient steam, ISteamSettingsService settings, TimeProvider clock)
{
    private const int MaxTitleLength = 200;

    public async Task<SteamSyncResultDto> SyncAsync(CancellationToken cancellationToken)
    {
        SteamCredentials credentials = await settings.GetCredentialsAsync(cancellationToken)
            ?? throw new SteamNotConfiguredException();

        IReadOnlyList<SteamOwnedGame> library = await steam.GetOwnedGamesAsync(credentials.ApiKey, credentials.SteamId, cancellationToken);
        IReadOnlyList<SteamWishlistEntry> wishlist = await steam.GetWishlistAsync(credentials.ApiKey, credentials.SteamId, cancellationToken);
        IReadOnlyDictionary<long, string> names = await steam.GetAppNamesAsync(wishlist.Select(w => w.AppId).ToList(), cancellationToken);

        db.SteamLibrary.RemoveRange(await db.SteamLibrary.ToListAsync(cancellationToken));
        db.Wishlist.RemoveRange(await db.Wishlist.ToListAsync(cancellationToken));

        db.SteamLibrary.AddRange(library
            .DistinctBy(g => g.AppId)
            .Select(g => new SteamLibraryItem { AppId = g.AppId, Title = g.Name, PlaytimeMinutes = g.PlaytimeMinutes }));
        db.Wishlist.AddRange(wishlist
            .DistinctBy(w => w.AppId)
            .Select(w => new WishlistItem
            {
                AppId = w.AppId,
                Title = names.GetValueOrDefault(w.AppId) ?? $"App {w.AppId}",
                AddedAt = w.AddedAt,
            }));

        int newGames = await AddMissingGamesAsync(library, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return new SteamSyncResultDto(library.Count, wishlist.Count, newGames);
    }

    // Owned Steam games also become entries of the main game list (as Backlog) so the whole library lives in one place.
    private async Task<int> AddMissingGamesAsync(IReadOnlyList<SteamOwnedGame> library, CancellationToken cancellationToken)
    {
        int steamId = PlatformCatalog.FindByName("Steam")!.Id;
        HashSet<string> existing = (await db.Games.Where(g => g.PlatformId == steamId).Select(g => g.Title).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DateTimeOffset now = clock.GetUtcNow();
        int added = 0;

        foreach (SteamOwnedGame owned in library.DistinctBy(g => g.AppId))
        {
            string title = owned.Name.Trim();
            if (title.Length == 0)
            {
                continue;
            }

            title = title[..Math.Min(title.Length, MaxTitleLength)];
            if (!existing.Add(title))
            {
                continue;
            }

            db.Games.Add(new Game { Title = title, PlatformId = steamId, Status = GameStatus.Backlog, CreatedAt = now, UpdatedAt = now });
            added++;
        }

        return added;
    }
}
