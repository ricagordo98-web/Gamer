using System.Xml;
using System.Xml.Linq;
using Gamer.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Library;

/// <summary>
/// Reads the game lists LaunchBox keeps in Data\Platforms\*.xml. Strictly read-only: nothing in the LaunchBox folder is written,
/// and its settings file (which holds account tokens) is never opened.
/// </summary>
public sealed class LaunchBoxScanner(IServiceScopeFactory scopes) : ILibraryScanner
{
    public const string SettingKey = "library.launchboxPath";

    private static readonly Dictionary<string, string> StorefrontPlatforms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["steam"] = "Steam",
        ["epic"] = "Epic",
        ["ea"] = "EA",
        ["origin"] = "EA",
        ["ubisoft"] = "Ubisoft",
        ["gog"] = "GOG",
        ["battlenet"] = "Battle.net",
        ["rockstar"] = "Rockstar",
    };

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "LaunchBox");

    public string Id => "launchbox";
    public string Name => "LaunchBox";
    public string Kind => "folder";
    public string Description => "Jogos de todas as lojas e consolas que já importaste no LaunchBox (só leitura).";
    public string? PathSettingKey => SettingKey;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken) =>
        Directory.Exists(PlatformsFolder(await GetPathAsync(cancellationToken) ?? DefaultPath));

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
        string folder = PlatformsFolder(root);
        if (!Directory.Exists(folder))
        {
            throw new LibraryScanException($"Não encontrei os dados do LaunchBox em {folder}.");
        }

        return await Task.Run(() => ScanFolder(folder, cancellationToken), cancellationToken);
    }

    public static IReadOnlyList<ScannedGame> ScanFolder(string platformsFolder, CancellationToken cancellationToken = default)
    {
        List<ScannedGame> games = [];
        foreach (string file in Directory.EnumerateFiles(platformsFolder, "*.xml").Order(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            games.AddRange(ReadFile(file));
        }

        return games;
    }

    internal static IEnumerable<ScannedGame> ReadFile(string file)
    {
        using XmlReader reader = XmlReader.Create(file, new XmlReaderSettings { CheckCharacters = false, DtdProcessing = DtdProcessing.Prohibit });
        reader.MoveToContent();
        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "Game")
            {
                reader.Read();
                continue;
            }

            XElement game = (XElement)XNode.ReadFrom(reader);
            string title = game.Element("Title")?.Value.Trim() ?? string.Empty;
            if (title.Length == 0 || game.Element("Hide")?.Value == "true")
            {
                continue;
            }

            if (ToGame(title, game.Element("Platform")?.Value ?? string.Empty, game.Element("StorefrontPluginId")?.Value ?? string.Empty) is { } scanned)
            {
                yield return scanned;
            }
        }
    }

    internal static ScannedGame? ToGame(string title, string launchBoxPlatform, string storefrontPluginId)
    {
        foreach ((string key, string platform) in StorefrontPlatforms)
        {
            if (storefrontPluginId.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return new ScannedGame(title, platform, PlatformCatalog.PcFamily);
            }
        }

        if (launchBoxPlatform.Length == 0 || launchBoxPlatform.StartsWith("PC", StringComparison.OrdinalIgnoreCase)
            || launchBoxPlatform is "Windows" or "MS-DOS")
        {
            return null;
        }

        return ConsolePlatforms.Resolve(title, launchBoxPlatform);
    }

    private static string PlatformsFolder(string root) => Path.Combine(root, "Data", "Platforms");
}
