using Gamer.Api.Data;

namespace Gamer.Api.Features.Library;

/// <summary>Maps the console names found in emulator folders and LaunchBox to the library's own platforms, so emulated games sit next to the real ones.</summary>
public static class ConsolePlatforms
{
    public const string EmulatorSuffix = " (Emu)";

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["psx"] = "PS1",
        ["ps1"] = "PS1",
        ["playstation"] = "PS1",
        ["sony playstation"] = "PS1",
        ["ps2"] = "PS2",
        ["sony playstation 2"] = "PS2",
        ["ps3"] = "PS3",
        ["sony playstation 3"] = "PS3",
        ["ps4"] = "PS4",
        ["sony playstation 4"] = "PS4",
        ["psp"] = "PSP",
        ["sony psp"] = "PSP",
        ["nes"] = "NES",
        ["nintendo entertainment system"] = "NES",
        ["snes"] = "SNES",
        ["super nintendo entertainment system"] = "SNES",
        ["n64"] = "N64",
        ["nintendo 64"] = "N64",
        ["gc"] = "GameCube",
        ["gamecube"] = "GameCube",
        ["nintendo gamecube"] = "GameCube",
        ["wii"] = "Wii",
        ["nintendo wii"] = "Wii",
        ["wiiu"] = "Wii U",
        ["nintendo wii u"] = "Wii U",
        ["nds"] = "NDS",
        ["nintendo ds"] = "NDS",
        ["3ds"] = "3DS",
        ["nintendo 3ds"] = "3DS",
        ["gba"] = "GBA",
        ["nintendo game boy advance"] = "GBA",
        ["game boy advance"] = "GBA",
        ["switch"] = "Switch",
        ["nintendo switch"] = "Switch",
    };

    public static ScannedGame Resolve(string title, string rawConsole)
    {
        string raw = rawConsole.EndsWith(EmulatorSuffix, StringComparison.OrdinalIgnoreCase) ? rawConsole[..^EmulatorSuffix.Length] : rawConsole;
        string name = Aliases.GetValueOrDefault(raw.Trim(), raw.Trim());
        return new ScannedGame(title, name, FamilyOf(name));
    }

    public static string FamilyOf(string name)
    {
        if (PlatformCatalog.FindByName(name) is { } known)
        {
            return known.Family;
        }

        return name switch
        {
            "Wii U" or "3DS" or "GBA" or "Switch" => PlatformCatalog.NintendoFamily,
            _ when name.StartsWith("PS", StringComparison.OrdinalIgnoreCase) => PlatformCatalog.PlayStationFamily,
            _ => PlatformCatalog.OtherFamily,
        };
    }
}
