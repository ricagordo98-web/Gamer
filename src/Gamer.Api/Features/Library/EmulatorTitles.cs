using System.Text.RegularExpressions;

namespace Gamer.Api.Features.Library;

public static partial class EmulatorTitles
{
    private static readonly string[] SubExtensions = [".nkit", ".iso", ".gcz", ".rvz"];

    /// <summary>Turns a ROM/ISO name such as "Mario Kart (Europe) (En,Fr).nkit.iso" into "Mario Kart".</summary>
    public static string Clean(string name, bool hasExtension = true)
    {
        string text = hasExtension ? Path.GetFileNameWithoutExtension(name) : name;
        foreach (string extra in SubExtensions)
        {
            if (text.EndsWith(extra, StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^extra.Length];
            }
        }

        text = TagGroups().Replace(text, " ");
        text = SerialSuffix().Replace(text, string.Empty);
        return Whitespace().Replace(text, " ").Trim(' ', '-', '.');
    }

    [GeneratedRegex(@"[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex TagGroups();

    [GeneratedRegex(@"\s+[A-Z]{4}\d{5}\s*$")]
    private static partial Regex SerialSuffix();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
