using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Gamer.Api.Features.Library;

public record InstalledProgram(string DisplayName, string? Publisher);

public static class InstalledPrograms
{
    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallKey32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public static IReadOnlyList<InstalledProgram> Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        List<InstalledProgram> programs = [];
        ReadFrom(Registry.LocalMachine, UninstallKey, programs);
        ReadFrom(Registry.LocalMachine, UninstallKey32, programs);
        ReadFrom(Registry.CurrentUser, UninstallKey, programs);
        return programs;
    }

    /// <summary>Keeps the programs of one publisher, dropping launchers/tools by name.</summary>
    public static IEnumerable<string> GamesOf(IEnumerable<InstalledProgram> programs, string publisher, params string[] excludedNames) =>
        programs
            .Where(p => p.Publisher is not null && p.Publisher.Contains(publisher, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.DisplayName.Trim())
            .Where(name => name.Length > 0
                && !excludedNames.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase))
                && !name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> ReadGogGames() =>
        OperatingSystem.IsWindows() ? ReadGogGamesWindows() : [];

    [SupportedOSPlatform("windows")]
    private static void ReadFrom(RegistryKey root, string path, List<InstalledProgram> into)
    {
        using RegistryKey? key = root.OpenSubKey(path);
        if (key is null)
        {
            return;
        }

        foreach (string name in key.GetSubKeyNames())
        {
            using RegistryKey? app = key.OpenSubKey(name);
            if (app?.GetValue("DisplayName") is string displayName && displayName.Length > 0)
            {
                into.Add(new InstalledProgram(displayName, app.GetValue("Publisher") as string));
            }
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<string> ReadGogGamesWindows()
    {
        List<string> names = [];
        using RegistryKey? games = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\GOG.com\Games");
        if (games is null)
        {
            return names;
        }

        foreach (string id in games.GetSubKeyNames())
        {
            using RegistryKey? game = games.OpenSubKey(id);
            if (game?.GetValue("gameName") is string name && name.Length > 0)
            {
                names.Add(name.Trim());
            }
        }

        return names;
    }
}
