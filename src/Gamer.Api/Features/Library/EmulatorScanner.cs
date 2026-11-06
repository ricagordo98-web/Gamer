using Gamer.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Library;

/// <summary>
/// Reads an emulator games folder laid out as root\Console\game files or folders.
/// It only lists names: nothing in that folder is ever written, moved or deleted.
/// </summary>
public sealed class EmulatorScanner(IServiceScopeFactory scopes) : ILibraryScanner
{
    public const string DefaultPath = @"E:\Ricardo\Jogos\Emulator";
    public const string SettingKey = "library.emulatorPath";
    public const string EmulatorFamily = "Emulador";

    private static readonly HashSet<string> GameExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".iso", ".chd", ".cue", ".zip", ".7z", ".rar", ".cso", ".pbp", ".wbfs", ".gcz", ".rvz", ".nkit",
        ".z64", ".v64", ".n64", ".nds", ".3ds", ".sfc", ".smc", ".nes", ".gba", ".gb", ".gbc", ".md", ".gen", ".sms", ".gg",
    };

    private static readonly Dictionary<string, string> ConsoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gamecube"] = "GameCube",
        ["wii"] = "Wii",
        ["wiiu"] = "Wii U",
        ["nds"] = "NDS",
        ["gba"] = "GBA",
        ["ps1"] = "PS1",
        ["psx"] = "PS1",
        ["ps2"] = "PS2",
        ["ps3"] = "PS3",
        ["psp"] = "PSP",
        ["n64"] = "N64",
        ["snes"] = "SNES",
        ["nes"] = "NES",
    };

    public string Id => "emulator";
    public string Name => "Emuladores";
    public string Kind => "folder";
    public string Description => "Jogos da pasta de emuladores (só leitura: nada é apagado nem alterado).";
    public string? PathSettingKey => SettingKey;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        Directory.Exists(await GetPathAsync(cancellationToken));

    public async Task<string?> GetPathAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopes.CreateScope();
        GamerDbContext db = scope.ServiceProvider.GetRequiredService<GamerDbContext>();
        string? saved = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == SettingKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(saved) ? DefaultPath : saved;
    }

    public async Task<IReadOnlyList<ScannedGame>> ScanAsync(CancellationToken cancellationToken)
    {
        string root = await GetPathAsync(cancellationToken) ?? DefaultPath;
        if (!Directory.Exists(root))
        {
            throw new LibraryScanException($"Pasta não encontrada: {root}");
        }

        return await Task.Run(() => ScanFolder(root), cancellationToken);
    }

    public static IReadOnlyList<ScannedGame> ScanFolder(string root)
    {
        List<ScannedGame> games = [];
        foreach (string consoleFolder in Directory.EnumerateDirectories(root))
        {
            string folderName = Path.GetFileName(consoleFolder);
            if (folderName.StartsWith('.') || folderName.Contains("BIOS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string platform = $"{ConsoleNames.GetValueOrDefault(folderName, folderName)} (Emu)";
            HashSet<string> titles = new(StringComparer.OrdinalIgnoreCase);
            foreach (string entry in Directory.EnumerateFileSystemEntries(consoleFolder))
            {
                bool isFolder = Directory.Exists(entry);
                if (!isFolder && !GameExtensions.Contains(Path.GetExtension(entry)))
                {
                    continue;
                }

                string title = EmulatorTitles.Clean(Path.GetFileName(entry), hasExtension: !isFolder);
                if (title.Length > 0 && titles.Add(title))
                {
                    games.Add(new ScannedGame(title, platform, EmulatorFamily));
                }
            }
        }

        return games;
    }
}
