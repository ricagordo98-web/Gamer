using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Contracts.Games;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Library;

/// <summary>
/// Keeps the library tidy: folds the old "X (Emu)" platforms into the real console platform and removes plain duplicates.
/// It never deletes a game that has progress, a rating, notes or is on the Deck: those are always kept.
/// </summary>
public static class LibraryMaintenance
{
    public static async Task<int> TidyAsync(GamerDbContext db, CancellationToken cancellationToken)
    {
        int changes = await MergeEmulatorPlatformsAsync(db, cancellationToken);
        changes += await DuplicateService.AutoMergeAsync(db, cancellationToken);
        return changes;
    }

    private static async Task<int> MergeEmulatorPlatformsAsync(GamerDbContext db, CancellationToken cancellationToken)
    {
        List<Platform> emulated = await db.Platforms
            .Where(p => p.Name.EndsWith(ConsolePlatforms.EmulatorSuffix) || p.Family == "Emulador")
            .ToListAsync(cancellationToken);
        int changes = 0;

        foreach (Platform source in emulated)
        {
            ScannedGame target = ConsolePlatforms.Resolve(string.Empty, source.Name);
            Platform? destination = await db.Platforms.FirstOrDefaultAsync(p => p.Name == target.PlatformName && p.Id != source.Id, cancellationToken);
            if (destination is null)
            {
                source.Name = target.PlatformName;
                source.Family = target.PlatformFamily;
                changes++;
                continue;
            }

            List<Game> moving = await db.Games.Where(g => g.PlatformId == source.Id).ToListAsync(cancellationToken);
            foreach (Game game in moving)
            {
                game.Platform = destination;
                game.PlatformId = destination.Id;
                changes++;
            }

            db.Platforms.Remove(source);
        }

        if (changes > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return changes;
    }

}
