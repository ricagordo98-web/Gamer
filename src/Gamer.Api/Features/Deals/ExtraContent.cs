using System.Text.RegularExpressions;

namespace Gamer.Api.Features.Deals;

public static partial class ExtraContent
{
    public static bool IsExtra(string title) => Markers().IsMatch(title);

    [GeneratedRegex(@"\b(dlc|demo|soundtrack|ost|season pass|expansion pass|artbook|art book|bundle pack|add-on|addon|skin pack|costume|character pack|cosmetic|supporter pack|starter pack|robux|riot points|gift card|v-bucks|vbucks|wallet|prepaid|top-up)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Markers();
}
