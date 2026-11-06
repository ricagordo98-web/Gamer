using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Steam;
using Gamer.Contracts.Games;
using Gamer.Contracts.Library;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Library;

public class UnknownSourceException(string id) : Exception($"Fonte desconhecida: {id}");

public class LibrarySourceService(
    GamerDbContext db,
    IEnumerable<ILibraryScanner> scanners,
    SteamSyncService steamSync,
    ISteamSettingsService steamSettings,
    TimeProvider clock)
{
    public const string SteamId = "steam";

    private const int MaxTitleLength = 200;

    public async Task<IReadOnlyList<LibrarySourceDto>> ListAsync(CancellationToken cancellationToken)
    {
        List<LibrarySourceDto> sources =
        [
            new(SteamId, "Steam", "account", "Biblioteca e wishlist da tua conta Steam (API key + SteamID nas Definições).",
                null, await steamSettings.GetCredentialsAsync(cancellationToken) is not null),
        ];

        foreach (ILibraryScanner scanner in scanners)
        {
            sources.Add(new LibrarySourceDto(
                scanner.Id,
                scanner.Name,
                scanner.Kind,
                scanner.Description,
                await scanner.GetPathAsync(cancellationToken),
                await scanner.IsAvailableAsync(cancellationToken)));
        }

        return sources;
    }

    public async Task<LibrarySyncResultDto> SyncAsync(string id, CancellationToken cancellationToken)
    {
        if (string.Equals(id, SteamId, StringComparison.OrdinalIgnoreCase))
        {
            Gamer.Contracts.Steam.SteamSyncResultDto steam = await steamSync.SyncAsync(cancellationToken);
            return new LibrarySyncResultDto(steam.LibraryCount, steam.NewGames);
        }

        ILibraryScanner scanner = scanners.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new UnknownSourceException(id);

        IReadOnlyList<ScannedGame> found = await scanner.ScanAsync(cancellationToken);
        int added = await AddMissingAsync(found, cancellationToken);
        return new LibrarySyncResultDto(found.Count, added);
    }

    public async Task SaveEmulatorPathAsync(string path, CancellationToken cancellationToken)
    {
        string trimmed = path.Trim();
        AppSetting? setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == EmulatorScanner.SettingKey, cancellationToken);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = EmulatorScanner.SettingKey, Value = trimmed });
        }
        else
        {
            setting.Value = trimmed;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> AddMissingAsync(IReadOnlyList<ScannedGame> found, CancellationToken cancellationToken)
    {
        Dictionary<string, Platform> platforms = await db.Platforms.ToDictionaryAsync(p => p.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        HashSet<string> known = (await db.Games
                .Select(g => new { g.Title, PlatformName = g.Platform.Name })
                .ToListAsync(cancellationToken))
            .Select(g => $"{g.Title}|{g.PlatformName}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        DateTimeOffset now = clock.GetUtcNow();
        int added = 0;
        foreach (ScannedGame game in found)
        {
            string title = game.Title.Trim();
            if (title.Length == 0)
            {
                continue;
            }

            title = title[..Math.Min(title.Length, MaxTitleLength)];
            if (!known.Add($"{title}|{game.PlatformName}"))
            {
                continue;
            }

            if (!platforms.TryGetValue(game.PlatformName, out Platform? platform))
            {
                platform = new Platform { Name = game.PlatformName, Family = game.PlatformFamily, Format = PlatformFormat.Digital };
                db.Platforms.Add(platform);
                platforms[game.PlatformName] = platform;
            }

            db.Games.Add(new Game { Title = title, Platform = platform, Status = GameStatus.Backlog, CreatedAt = now, UpdatedAt = now });
            added++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return added;
    }
}
