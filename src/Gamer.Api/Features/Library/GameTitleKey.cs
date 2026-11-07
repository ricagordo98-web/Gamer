using System.Text;
using System.Text.RegularExpressions;

namespace Gamer.Api.Features.Library;

/// <summary>
/// Builds a canonical key for a game title so that "COD 3", "Call of Duty 3" and "Call of Duty 3 (Europe)" compare equal,
/// and measures how alike two titles are for the "probably the same game" suggestions.
/// </summary>
public static partial class GameTitleKey
{
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.Ordinal)
    {
        ["cod"] = "call of duty",
        ["gta"] = "grand theft auto",
        ["nfs"] = "need for speed",
        ["mw"] = "modern warfare",
        ["bo"] = "black ops",
        ["waw"] = "world at war",
        ["mgs"] = "metal gear solid",
        ["dmc"] = "devil may cry",
        ["gow"] = "god of war",
        ["mk"] = "mortal kombat",
        ["ff"] = "final fantasy",
        ["kh"] = "kingdom hearts",
        ["pes"] = "pro evolution soccer",
        ["tlou"] = "the last of us",
        ["rdr"] = "red dead redemption",
        ["ww2"] = "wwii",
        ["wwii"] = "wwii",
    };

    private static readonly Dictionary<string, string> Romans = new(StringComparer.Ordinal)
    {
        ["ii"] = "2", ["iii"] = "3", ["iv"] = "4", ["vi"] = "6", ["vii"] = "7", ["viii"] = "8", ["ix"] = "9",
    };

    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "of", "and", "edition", "deluxe", "goty", "standard", "digital", "game", "year", "version", "ultimate", "complete",
    };

    /// <summary>Equal keys mean the same game (same platform assumed).</summary>
    public static string Create(string title)
    {
        List<string> words = Words(title);
        return string.Join(' ', words);
    }

    /// <summary>Digit tokens of a key (sequels, years): two titles with different numbers are different games.</summary>
    public static string Numbers(string key) =>
        string.Join(' ', key.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Any(char.IsDigit)).Order(StringComparer.Ordinal));

    /// <summary>0..1: how alike two keys are, tolerating typos and a different word order.</summary>
    public static double Similarity(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
        {
            return 0;
        }

        double direct = 1 - (double)Distance(left, right) / Math.Max(left.Length, right.Length);
        string sortedLeft = Sorted(left);
        string sortedRight = Sorted(right);
        double reordered = 1 - (double)Distance(sortedLeft, sortedRight) / Math.Max(sortedLeft.Length, sortedRight.Length);
        return Math.Max(direct, reordered);
    }

    private static List<string> Words(string title)
    {
        title = DottedLetters().Replace(title, string.Empty);
        string text = Brackets().Replace(title.ToLowerInvariant(), match => Year().IsMatch(match.Value) ? " " + Year().Match(match.Value).Value + " " : " ");
        text = text.Replace("&", " and ").Replace("'", string.Empty).Replace("’", string.Empty);

        StringBuilder builder = new(text.Length);
        foreach (char c in text)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        List<string> words = [];
        foreach (string raw in builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (string piece in SplitAbbreviation(raw))
            {
                string word = Romans.GetValueOrDefault(piece, piece);
                if (Abbreviations.TryGetValue(word, out string? full))
                {
                    words.AddRange(full.Split(' '));
                }
                else
                {
                    words.Add(word);
                }
            }
        }

        return words.Where(w => !Ignored.Contains(w)).ToList();
    }

    private static IEnumerable<string> SplitAbbreviation(string token)
    {
        Match match = LettersThenDigits().Match(token);
        if (match.Success && Abbreviations.ContainsKey(match.Groups[1].Value))
        {
            return [match.Groups[1].Value, match.Groups[2].Value];
        }

        return [token];
    }

    private static string Sorted(string key) => string.Concat(key.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal));

    // Optimal string alignment distance: an adjacent swap ("nigthfire") counts as one edit.
    private static int Distance(string a, string b)
    {
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"(?<=\p{L})\.(?=\p{L})")]
    private static partial Regex DottedLetters();

    [GeneratedRegex(@"\b(19|20)\d{2}\b")]
    private static partial Regex Year();

    [GeneratedRegex(@"^([a-z]+)(\d+)$")]
    private static partial Regex LettersThenDigits();
}
