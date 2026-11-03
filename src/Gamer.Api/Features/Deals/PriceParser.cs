using System.Globalization;
using System.Text.RegularExpressions;

namespace Gamer.Api.Features.Deals;

public static partial class PriceParser
{
    public static bool TryParse(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Match match = Number().Match(text);
        if (!match.Success)
        {
            return false;
        }

        string raw = match.Value.Replace(' ', ' ').Replace(" ", string.Empty);
        int lastSeparator = raw.LastIndexOfAny(['.', ',']);
        if (lastSeparator < 0)
        {
            return decimal.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out amount);
        }

        string integerPart = raw[..lastSeparator].Replace(".", string.Empty).Replace(",", string.Empty);
        string fraction = raw[(lastSeparator + 1)..];
        string normalized = fraction.Length == 3 ? integerPart + fraction : $"{integerPart}.{fraction}";
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
    }

    [GeneratedRegex(@"\d[\d.,  ]*\d|\d")]
    private static partial Regex Number();
}
