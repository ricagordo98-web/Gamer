using System.Net;
using System.Text.RegularExpressions;

namespace Gamer.Api.Features.Deals;

public static partial class TitleCleaner
{
    public static string Clean(string? title)
    {
        string text = WebUtility.HtmlDecode(title ?? string.Empty).Trim();
        string previous;
        do
        {
            previous = text;
            text = TrailingStore().Replace(text, string.Empty);
            text = DashPlatform().Replace(text, string.Empty);
            text = TrailingPlatform().Replace(text, string.Empty);
            text = KeyWords().Replace(text, string.Empty);
            text = text.Trim();
        }
        while (text != previous);

        return text;
    }

    [GeneratedRegex(@"\s*[\(\[]\s*(steam|epic games|epic|gog|gog\.com|microsoft store|xbox[^)\]]*|ea app|origin|ubisoft connect|battle\.net|rockstar|nintendo[^)\]]*|playstation[^)\]]*|eu|row|eu/row|global|key|digital[^)\]]*)\s*[\)\]]\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingStore();

    [GeneratedRegex(@"\s+[-–]\s*(pc|mac|linux|windows)\b.*$", RegexOptions.IgnoreCase)]
    private static partial Regex DashPlatform();

    [GeneratedRegex(@"\s+(pc|mac|linux)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TrailingPlatform();

    [GeneratedRegex(@"\s+(steam|epic|gog)(\s+(cd\s+)?(key|gift|account))?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex KeyWords();
}
