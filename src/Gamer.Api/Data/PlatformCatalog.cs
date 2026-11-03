using Gamer.Api.Domain;
using Gamer.Contracts.Games;

namespace Gamer.Api.Data;

public static class PlatformCatalog
{
    public const string PlayStationFamily = "PlayStation";
    public const string NintendoFamily = "Nintendo";
    public const string PcFamily = "PC";
    public const string OtherFamily = "Other";

    public static IReadOnlyList<Platform> All { get; } =
    [
        new() { Id = 1, Name = "PS1", Family = PlayStationFamily, Format = PlatformFormat.Physical },
        new() { Id = 2, Name = "PS2", Family = PlayStationFamily, Format = PlatformFormat.Physical },
        new() { Id = 3, Name = "PS3", Family = PlayStationFamily, Format = PlatformFormat.Physical },
        new() { Id = 4, Name = "PSP", Family = PlayStationFamily, Format = PlatformFormat.Physical },
        new() { Id = 5, Name = "PS4", Family = PlayStationFamily, Format = PlatformFormat.Digital },
        new() { Id = 6, Name = "NES", Family = NintendoFamily, Format = PlatformFormat.Physical },
        new() { Id = 7, Name = "SNES", Family = NintendoFamily, Format = PlatformFormat.Physical },
        new() { Id = 8, Name = "N64", Family = NintendoFamily, Format = PlatformFormat.Physical },
        new() { Id = 9, Name = "Wii", Family = NintendoFamily, Format = PlatformFormat.Physical },
        new() { Id = 10, Name = "GameCube", Family = NintendoFamily, Format = PlatformFormat.Physical },
        new() { Id = 11, Name = "NDS", Family = NintendoFamily, Format = PlatformFormat.Physical },
        new() { Id = 12, Name = "Steam", Family = PcFamily, Format = PlatformFormat.Digital },
        new() { Id = 13, Name = "EA", Family = PcFamily, Format = PlatformFormat.Digital },
        new() { Id = 14, Name = "Epic", Family = PcFamily, Format = PlatformFormat.Digital },
        new() { Id = 15, Name = "GOG", Family = PcFamily, Format = PlatformFormat.Digital },
        new() { Id = 16, Name = "Cracked", Family = PcFamily, Format = PlatformFormat.Digital },
    ];

    public static Platform? FindByName(string name) =>
        All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
}
