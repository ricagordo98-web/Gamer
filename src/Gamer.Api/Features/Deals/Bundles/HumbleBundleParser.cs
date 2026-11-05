using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals.Bundles;

public record HumbleListing(string Title, string Url, string? ImageUrl, string? Blurb, DateTimeOffset? EndsAt);

public static class HumbleBundleParser
{
    private const string GameContentType = "game";

    public static IReadOnlyList<HumbleListing> ParseListing(string html, string baseUrl)
    {
        using JsonDocument document = JsonDocument.Parse(ScriptJson(html, "landingPage-json-data"));
        if (!document.RootElement.TryGetProperty("data", out JsonElement data) || !data.TryGetProperty("games", out JsonElement games))
        {
            return [];
        }

        return games.GetProperty("mosaic").EnumerateArray()
            .SelectMany(section => section.TryGetProperty("products", out JsonElement products) ? products.EnumerateArray() : [])
            .Where(product => Text(product, "category") == "bundle" && Text(product, "product_url") is not null)
            .Select(product => new HumbleListing(
                WebUtility.HtmlDecode(Text(product, "tile_name") ?? Text(product, "tile_short_name") ?? string.Empty),
                baseUrl + Text(product, "product_url")!.TrimStart('/'),
                Text(product, "high_res_tile_image") ?? Text(product, "tile_image"),
                Text(product, "short_marketing_blurb"),
                Date(Text(product, "end_date|datetime"))))
            .Where(listing => listing.Title.Length > 0)
            .DistinctBy(listing => listing.Url)
            .ToList();
    }

    public static StoreBundle? ParseBundle(string html, HumbleListing listing)
    {
        using JsonDocument document = JsonDocument.Parse(ScriptJson(html, "webpack-bundle-page-data"));
        JsonElement bundle = document.RootElement.GetProperty("bundleData");
        JsonElement pricing = bundle.GetProperty("tier_pricing_data");
        JsonElement display = bundle.GetProperty("tier_display_data");
        JsonElement itemData = bundle.GetProperty("tier_item_data");
        string currency = bundle.TryGetProperty("basic_data", out JsonElement basic) ? Text(basic, "currency") ?? "EUR" : "EUR";

        List<(decimal Price, List<string> Items)> tiers = bundle.GetProperty("tier_order").EnumerateArray()
            .Select(tier => tier.GetString()!)
            .Where(id => pricing.TryGetProperty(id, out _) && display.TryGetProperty(id, out _))
            .Select(id => (Price: TierPrice(pricing.GetProperty(id)), Items: TierItems(display.GetProperty(id))))
            .Where(tier => tier.Price > 0)
            .OrderBy(tier => tier.Price)
            .ToList();

        Dictionary<string, int> firstTier = [];
        for (int index = 0; index < tiers.Count; index++)
        {
            foreach (string name in tiers[index].Items)
            {
                firstTier.TryAdd(name, index);
            }
        }

        List<StoreBundleItem> items = firstTier
            .Where(pair => itemData.TryGetProperty(pair.Key, out JsonElement item) && Text(item, "item_content_type") == GameContentType)
            .Select(pair => ToItem(itemData.GetProperty(pair.Key), pair.Value))
            .OrderBy(item => item.Tier)
            .ThenByDescending(item => item.Msrp ?? 0)
            .ToList();

        if (tiers.Count == 0 || items.Count == 0)
        {
            return null;
        }

        List<BundleTierDto> tierDtos = tiers
            .Select((tier, index) => new BundleTierDto(tier.Price, items.Count(item => item.Tier <= index)))
            .ToList();

        return new StoreBundle(listing.Title, listing.Url, listing.ImageUrl, listing.Blurb, listing.EndsAt, currency, BundleKinds.Tiered, tierDtos, items);
    }

    private static StoreBundleItem ToItem(JsonElement item, int tier)
    {
        string? image = item.TryGetProperty("resolved_paths", out JsonElement paths) ? Text(paths, "featured_image") : null;
        decimal? msrp = item.TryGetProperty("msrp_price|money", out JsonElement money) && money.ValueKind == JsonValueKind.Object
            ? money.GetProperty("amount").GetDecimal()
            : null;
        return new StoreBundleItem(WebUtility.HtmlDecode(Text(item, "human_name") ?? string.Empty), image, msrp, tier);
    }

    private static decimal TierPrice(JsonElement tier)
    {
        decimal price = Amount(tier, "price|money");
        return price > 0 ? price : Amount(tier, "average_purchase_price|money");
    }

    private static decimal Amount(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement money) && money.ValueKind == JsonValueKind.Object
            ? money.GetProperty("amount").GetDecimal()
            : 0;

    private static List<string> TierItems(JsonElement tier) =>
        new[] { "tier_item_machine_names", "bonus_item_machine_names" }
            .Where(property => tier.TryGetProperty(property, out JsonElement names) && names.ValueKind == JsonValueKind.Array)
            .SelectMany(property => tier.GetProperty(property).EnumerateArray().Select(name => name.GetString()!))
            .ToList();

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static DateTimeOffset? Date(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date) ? date : null;

    private static string ScriptJson(string html, string id)
    {
        Match match = Regex.Match(html, $"<script id=\"{Regex.Escape(id)}\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value : throw new ScrapeException($"The page has no {id} data; the store layout may have changed.");
    }
}
