using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Steam;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Steam;

public class SteamSyncService(GamerDbContext db, ISteamClient steam, ISteamSettingsService settings)
{
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

        await db.SaveChangesAsync(cancellationToken);
        return new SteamSyncResultDto(library.Count, wishlist.Count);
    }
}
