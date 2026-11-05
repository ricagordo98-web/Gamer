using System.Text.Json;
using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals.Bundles;

public static class FanaticalBundleParser
{
    private const string GameBundleType = "bundle";
    private const string ImageBase = "https://fanatical.imgix.net/product/original/";

    public static IReadOnlyList<StoreBundle> Parse(string json, string baseUrl, string currency)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("pickandmix", out JsonElement bundles) || bundles.ValueKind != JsonValueKind.Array)
        {
            throw new ParseException("The Fanatical response has no pick-and-mix bundles.");
        }

        return bundles.EnumerateArray()
            .Where(bundle => Text(bundle, "type") == GameBundleType && Text(bundle, "slug") is not null)
            .Select(bundle => ToBundle(bundle, baseUrl, currency))
            .Where(bundle => bundle.Tiers.Count > 0 && bundle.Items.Count > 0)
            .ToList();
    }

    private static StoreBundle ToBundle(JsonElement bundle, string baseUrl, string currency)
    {
        List<BundleTierDto> tiers = bundle.TryGetProperty("tiers", out JsonElement tierArray)
            ? tierArray.EnumerateArray()
                .Where(tier => tier.TryGetProperty("price", out JsonElement price) && price.TryGetProperty(currency, out _))
                .Select(tier => new BundleTierDto(tier.GetProperty("price").GetProperty(currency).GetDecimal() / 100m, tier.GetProperty("quantity").GetInt32()))
                .OrderBy(tier => tier.Count)
                .ToList()
            : [];

        List<StoreBundleItem> items = bundle.TryGetProperty("products", out JsonElement products)
            ? products.EnumerateArray()
                .Select(product => new StoreBundleItem(Text(product, "name") ?? string.Empty, Image(Text(product, "cover"), 400, 225), null, 0))
                .Where(item => item.Title.Length > 0)
                .ToList()
            : [];

        return new StoreBundle(
            Text(bundle, "name") ?? string.Empty,
            $"{baseUrl}en/pick-and-mix/{Text(bundle, "slug")}",
            Image(Text(bundle, "cover_image"), 1200, 675),
            null,
            null,
            currency,
            BundleKinds.Pick,
            tiers,
            items);
    }

    private static string? Image(string? file, int width, int height) =>
        file is null ? null : $"{ImageBase}{file}?auto=compress,format&w={width}&fit=crop&h={height}";

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
