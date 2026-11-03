using System.Text;
using System.Text.RegularExpressions;

namespace Gamer.Api.Features.Deals;

public static partial class TitleMatcher
{
    private const double MinimumSimilarity = 0.88;

    public static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        StringBuilder builder = new(title.Length);
        foreach (char c in title.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        string text = NoiseWords().Replace(builder.ToString(), " ");
        return MultipleSpaces().Replace(text, " ").Trim();
    }

    public static bool IsMatch(string query, string candidate)
    {
        string left = Normalize(query);
        string right = Normalize(candidate);
        if (left.Length == 0 || right.Length == 0)
        {
            return false;
        }

        if (left == right)
        {
            return true;
        }

        return SameNumbers(left, right) && Similarity(left, right) >= MinimumSimilarity;
    }

    private static bool SameNumbers(string left, string right) =>
        Numbers(left).SequenceEqual(Numbers(right));

    private static IEnumerable<string> Numbers(string text) =>
        text.Split(' ').Where(IsNumberToken).Order();

    private static bool IsNumberToken(string token) =>
        token.All(char.IsDigit) || RomanNumeral().IsMatch(token);

    private static double Similarity(string left, string right)
    {
        List<string> a = Bigrams(left);
        List<string> b = Bigrams(right);
        int common = 0;
        foreach (string gram in a)
        {
            if (b.Remove(gram))
            {
                common++;
            }
        }

        int total = a.Count + common + b.Count;
        return total == 0 ? 0 : 2d * common / (total - common + common);
    }

    private static List<string> Bigrams(string text)
    {
        List<string> grams = [];
        for (int i = 0; i < text.Length - 1; i++)
        {
            grams.Add(text.Substring(i, 2));
        }

        return grams;
    }

    [GeneratedRegex(@"\b(edition|deluxe|goty|game of the year|standard|digital|steam|pc)\b")]
    private static partial Regex NoiseWords();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpaces();

    [GeneratedRegex("^(ii|iii|iv|v|vi|vii|viii|ix|x)$")]
    private static partial Regex RomanNumeral();
}
