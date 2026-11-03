using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Games;

namespace Gamer.Api.Features.Import;

public record ParsedPlatform(string Name, string Family, PlatformFormat Format);

public static class PlatformParser
{
    private const string PhysicalPrefix = "L -";
    private const string PcPrefix = "PC -";

    public static ParsedPlatform Parse(string rawPlatform)
    {
        string text = rawPlatform.Trim();
        bool isPhysical = text.StartsWith(PhysicalPrefix, StringComparison.OrdinalIgnoreCase);
        bool isPc = text.StartsWith(PcPrefix, StringComparison.OrdinalIgnoreCase);

        string name = isPhysical ? text[PhysicalPrefix.Length..].Trim()
            : isPc ? text[PcPrefix.Length..].Trim()
            : text;

        Platform? known = PlatformCatalog.FindByName(name);
        if (known is not null)
        {
            PlatformFormat format = isPhysical ? PlatformFormat.Physical
                : isPc ? PlatformFormat.Digital
                : known.Format;
            return new ParsedPlatform(known.Name, known.Family, format);
        }

        return new ParsedPlatform(
            name,
            isPc ? PlatformCatalog.PcFamily : PlatformCatalog.OtherFamily,
            isPhysical ? PlatformFormat.Physical : PlatformFormat.Digital);
    }
}
