using System.Text.RegularExpressions;

namespace Gamer.Api.Features.Deals.Parsers;

public static partial class CountdownParser
{
    public static DateTimeOffset? Parse(string? text, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        Match clock = Clock().Match(text);
        if (clock.Success)
        {
            return now + new TimeSpan(int.Parse(clock.Groups[1].Value), int.Parse(clock.Groups[2].Value), int.Parse(clock.Groups[3].Value));
        }

        TimeSpan left = TimeSpan.FromDays(Number(Days(), text)) + TimeSpan.FromHours(Number(Hours(), text)) + TimeSpan.FromMinutes(Number(Minutes(), text));
        return left > TimeSpan.Zero ? now + left : null;
    }

    private static int Number(Regex regex, string text)
    {
        Match match = regex.Match(text);
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    [GeneratedRegex(@"(\d{1,3}):(\d{2}):(\d{2})")]
    private static partial Regex Clock();

    [GeneratedRegex(@"(\d+)\s*(?:days?|d)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Days();

    [GeneratedRegex(@"(\d+)\s*(?:hours?|hrs?|h)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Hours();

    [GeneratedRegex(@"(\d+)\s*(?:minutes?|mins?|m)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Minutes();
}
