using System.Net;
using System.Text.Json;
using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Parsers;

public class HumbleSearchParser : IOfferParser
{
    public IReadOnlyList<StoreOffer> Parse(string content, Store store)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(JsonText.Extract(content));
            if (!document.RootElement.TryGetProperty("results", out JsonElement results) || results.ValueKind != JsonValueKind.Array)
            {
                throw new ParseException("Humble response has no results.");
            }

            List<StoreOffer> offers = [];
            foreach (JsonElement item in results.EnumerateArray())
            {
                if (TryRead(item, store) is { } offer)
                {
                    offers.Add(offer);
                }
            }

            return offers;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new ParseException("Humble response is not valid JSON.", exception);
        }
    }

    private static StoreOffer? TryRead(JsonElement item, Store store)
    {
        string? name = Text(item, "human_name");
        string? slug = Text(item, "human_url");
        if (name is null || slug is null || Amount(item, "current_price") is not { } price)
        {
            return null;
        }

        decimal original = Amount(item, "full_price") is { } full && full > price ? full : price;
        string currency = item.TryGetProperty("current_price", out JsonElement current) ? Text(current, "currency") ?? store.Currency : store.Currency;
        string? image = Text(item, "standard_carousel_image") ?? Text(item, "featured_image_recommendation") ?? Text(item, "large_capsule");
        DateTimeOffset? endsAt = original > price && item.TryGetProperty("sale_end", out JsonElement end) && end.TryGetDouble(out double seconds) && seconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds((long)seconds)
            : null;

        return new StoreOffer(store.Name, WebUtility.HtmlDecode(name), price, original, $"{store.BaseUrl}store/{slug}", currency, image, endsAt);
    }

    private static decimal? Amount(JsonElement item, string property) =>
        item.TryGetProperty(property, out JsonElement money) && money.ValueKind == JsonValueKind.Object
            && money.TryGetProperty("amount", out JsonElement amount) && amount.TryGetDecimal(out decimal value)
            ? value
            : null;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
