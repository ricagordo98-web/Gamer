using System.Net;
using System.Text.RegularExpressions;

namespace Gamer.Api.Features.Deals.Parsers;

public static partial class JsonText
{
    public static string Extract(string content)
    {
        string trimmed = content.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return trimmed;
        }

        Match match = PreBlock().Match(content);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Value) : trimmed;
    }

    [GeneratedRegex("<pre[^>]*>(.*?)</pre>", RegexOptions.Singleline)]
    private static partial Regex PreBlock();
}
