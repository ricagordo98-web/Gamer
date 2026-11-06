namespace Gamer.Api.Features.Library;

/// <summary>
/// Lists the games a PC launcher has installed on this machine. None of these launchers has a public account API,
/// so this reads what is installed locally (registry or launcher manifests).
/// </summary>
public sealed class LauncherScanner(string id, string platformName, string description, Func<IEnumerable<string>> readTitles) : ILibraryScanner
{
    public string Id => id;
    public string Name => platformName;
    public string Kind => "launcher";
    public string Description => description;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<string?> GetPathAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    public Task<IReadOnlyList<ScannedGame>> ScanAsync(CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<ScannedGame>>(() => readTitles()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(title => new ScannedGame(title, platformName, "PC"))
            .ToList(), cancellationToken);

    public static IReadOnlyList<ILibraryScanner> CreateAll() =>
    [
        new LauncherScanner("epic", "Epic", "Jogos instalados pelo Epic Games Launcher neste PC.", ReadEpic),
        new LauncherScanner("gog", "GOG", "Jogos GOG instalados neste PC (GOG Galaxy ou instaladores).",
            () => InstalledPrograms.ReadGogGames().Concat(InstalledPrograms.GamesOf(InstalledPrograms.Read(), "GOG", "GOG GALAXY", "Galaxy"))),
        new LauncherScanner("ubisoft", "Ubisoft", "Jogos instalados pelo Ubisoft Connect neste PC.",
            () => InstalledPrograms.GamesOf(InstalledPrograms.Read(), "Ubisoft", "Ubisoft Connect", "Uplay", "Launcher")),
        new LauncherScanner("ea", "EA", "Jogos instalados pela EA app / Origin neste PC.",
            () => InstalledPrograms.GamesOf(InstalledPrograms.Read(), "Electronic Arts", "EA app", "EA Desktop", "Origin", "EA Help", "EA Javelin")),
        new LauncherScanner("battlenet", "Battle.net", "Jogos Blizzard instalados pelo Battle.net neste PC.",
            () => InstalledPrograms.GamesOf(InstalledPrograms.Read(), "Blizzard Entertainment", "Battle.net")),
        new LauncherScanner("rockstar", "Rockstar", "Jogos Rockstar instalados neste PC.",
            () => InstalledPrograms.GamesOf(InstalledPrograms.Read(), "Rockstar Games", "Launcher", "Social Club")),
    ];

    private static string EpicManifests => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");

    private static IEnumerable<string> ReadEpic()
    {
        if (!Directory.Exists(EpicManifests))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(EpicManifests, "*.item"))
        {
            string? title = EpicManifestParser.ParseTitle(File.ReadAllText(file));
            if (title is not null)
            {
                yield return title;
            }
        }
    }
}
