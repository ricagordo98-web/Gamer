using System.Globalization;
using System.Text.Json;
using Gamer.Api.Features.Steam;

namespace Gamer.Api.Features.Stats;

public record SteamStoreDetails(decimal? Price, decimal? NormalPrice, string Currency, int DiscountPercent, int? Metacritic, IReadOnlyList<string> Genres, string? ReleaseDate);

public record SteamReviews(int? Percent, int Count, string? Summary);

public record SteamSpyData(string? Owners, int? AverageMinutes, IReadOnlyList<string> Tags, SteamReviews? Reviews = null);

public record SteamStoreInfo(SteamStoreDetails? Details, SteamReviews? Reviews, SteamSpyData? Spy);

public record SteamPrice(decimal Price, decimal NormalPrice, string Currency, int DiscountPercent);

public interface ISteamStoreClient
{
    Task<SteamStoreInfo> GetInfoAsync(long appId, CancellationToken cancellationToken);
    Task<int?> GetCurrentPlayersAsync(long appId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<long, SteamPrice>> GetPricesAsync(IReadOnlyCollection<long> appIds, CancellationToken cancellationToken);
}

public class SteamStoreClient(HttpClient http) : ISteamStoreClient
{
    private const int MaxTags = 8;

    public async Task<SteamStoreInfo> GetInfoAsync(long appId, CancellationToken cancellationToken)
    {
        SteamStoreDetails? details = null;
        SteamReviews? reviews = null;
        SteamSpyData? spy = null;

        using (JsonDocument? document = await TryGetJsonAsync($"https://store.steampowered.com/api/appdetails?appids={appId}&cc=pt&l=english", cancellationToken))
        {
            details = document is null ? null : ParseDetails(document.RootElement, appId);
        }

        using (JsonDocument? document = await TryGetJsonAsync($"https://store.steampowered.com/appreviews/{appId}?json=1&language=all&purchase_type=all&num_per_page=0", cancellationToken))
        {
            reviews = document is null ? null : ParseReviews(document.RootElement);
        }

        using (JsonDocument? document = await TryGetJsonAsync($"https://steamspy.com/api.php?request=appdetails&appid={appId}", cancellationToken))
        {
            spy = document is null ? null : ParseSpy(document.RootElement);
        }

        if (details is null && reviews is null && spy is null)
        {
            throw new SteamException("Não foi possível obter dados da Steam.");
        }

        return new SteamStoreInfo(details, reviews ?? spy?.Reviews, spy);
    }

    public async Task<IReadOnlyDictionary<long, SteamPrice>> GetPricesAsync(IReadOnlyCollection<long> appIds, CancellationToken cancellationToken)
    {
        using JsonDocument document = await TryGetJsonAsync(
            $"https://store.steampowered.com/api/appdetails?appids={string.Join(',', appIds)}&filters=price_overview&cc=pt", cancellationToken)
            ?? throw new SteamException("Não foi possível obter os preços da Steam.");
        return ParsePrices(document.RootElement);
    }

    public static Dictionary<long, SteamPrice> ParsePrices(JsonElement root)
    {
        Dictionary<long, SteamPrice> prices = [];
        if (root.ValueKind != JsonValueKind.Object)
        {
            return prices;
        }

        foreach (JsonProperty entry in root.EnumerateObject())
        {
            if (!long.TryParse(entry.Name, out long appId)
                || !entry.Value.TryGetProperty("success", out JsonElement success) || !success.GetBoolean()
                || !entry.Value.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("price_overview", out JsonElement overview))
            {
                continue;
            }

            prices[appId] = new SteamPrice(
                overview.GetProperty("final").GetInt32() / 100m,
                overview.GetProperty("initial").GetInt32() / 100m,
                overview.GetProperty("currency").GetString() ?? string.Empty,
                overview.TryGetProperty("discount_percent", out JsonElement percent) ? percent.GetInt32() : 0);
        }

        return prices;
    }

    public async Task<int?> GetCurrentPlayersAsync(long appId, CancellationToken cancellationToken)
    {
        using JsonDocument? document = await TryGetJsonAsync($"https://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid={appId}", cancellationToken);
        return document is not null
            && document.RootElement.TryGetProperty("response", out JsonElement response)
            && response.TryGetProperty("player_count", out JsonElement count)
            && count.TryGetInt32(out int players)
                ? players
                : null;
    }

    public static SteamStoreDetails? ParseDetails(JsonElement root, long appId)
    {
        if (!root.TryGetProperty(appId.ToString(CultureInfo.InvariantCulture), out JsonElement entry)
            || !entry.TryGetProperty("success", out JsonElement success) || !success.GetBoolean()
            || !entry.TryGetProperty("data", out JsonElement data))
        {
            return null;
        }

        decimal? price = null;
        decimal? normal = null;
        string currency = string.Empty;
        int discount = 0;
        if (data.TryGetProperty("price_overview", out JsonElement overview))
        {
            price = overview.GetProperty("final").GetInt32() / 100m;
            normal = overview.GetProperty("initial").GetInt32() / 100m;
            currency = overview.GetProperty("currency").GetString() ?? string.Empty;
            discount = overview.TryGetProperty("discount_percent", out JsonElement percent) ? percent.GetInt32() : 0;
        }
        else if (data.TryGetProperty("is_free", out JsonElement free) && free.GetBoolean())
        {
            price = 0m;
            normal = 0m;
        }

        int? metacritic = data.TryGetProperty("metacritic", out JsonElement meta) && meta.TryGetProperty("score", out JsonElement score)
            ? score.GetInt32()
            : null;

        List<string> genres = data.TryGetProperty("genres", out JsonElement genreList)
            ? genreList.EnumerateArray().Select(g => g.GetProperty("description").GetString() ?? string.Empty).Where(g => g.Length > 0).ToList()
            : [];

        string? release = data.TryGetProperty("release_date", out JsonElement releaseDate) && releaseDate.TryGetProperty("date", out JsonElement date)
            ? date.GetString()
            : null;

        return new SteamStoreDetails(price, normal, currency, discount, metacritic, genres, release);
    }

    public static SteamReviews? ParseReviews(JsonElement root)
    {
        if (!root.TryGetProperty("query_summary", out JsonElement summary))
        {
            return null;
        }

        int positive = summary.TryGetProperty("total_positive", out JsonElement pos) ? pos.GetInt32() : 0;
        int negative = summary.TryGetProperty("total_negative", out JsonElement neg) ? neg.GetInt32() : 0;
        int total = positive + negative;
        string? description = summary.TryGetProperty("review_score_desc", out JsonElement desc) ? desc.GetString() : null;

        return new SteamReviews(total == 0 ? null : (int)Math.Round(100.0 * positive / total), total, description);
    }

    public static SteamSpyData? ParseSpy(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? owners = root.TryGetProperty("owners", out JsonElement ownersElement) ? ownersElement.GetString()?.Replace(" .. ", " – ") : null;
        int? average = root.TryGetProperty("average_forever", out JsonElement averageElement) && averageElement.TryGetInt32(out int minutes) && minutes > 0
            ? minutes
            : null;
        List<string> tags = root.TryGetProperty("tags", out JsonElement tagElement) && tagElement.ValueKind == JsonValueKind.Object
            ? tagElement.EnumerateObject().OrderByDescending(t => t.Value.GetInt32()).Take(MaxTags).Select(t => t.Name).ToList()
            : [];

        int positive = root.TryGetProperty("positive", out JsonElement pos) && pos.TryGetInt32(out int p) ? p : 0;
        int negative = root.TryGetProperty("negative", out JsonElement neg) && neg.TryGetInt32(out int n) ? n : 0;
        SteamReviews? reviews = positive + negative == 0 ? null : new SteamReviews((int)Math.Round(100.0 * positive / (positive + negative)), positive + negative, null);

        return owners is null && average is null && tags.Count == 0 ? null : new SteamSpyData(owners, average, tags, reviews);
    }

    private async Task<JsonDocument?> TryGetJsonAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode
                ? await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken)
                : null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
