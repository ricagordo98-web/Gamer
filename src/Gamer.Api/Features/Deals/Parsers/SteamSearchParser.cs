using System.Text.Json;
using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Parsers;

public class SteamSearchParser : IOfferParser
{
    public IReadOnlyList<StoreOffer> Parse(string content, Store store)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(JsonText.Extract(content));
            if (!document.RootElement.TryGetProperty("items", out JsonElement items))
            {
                throw new ParseException("Steam response has no items.");
            }

            List<StoreOffer> offers = [];
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("price", out JsonElement price)
                    || !price.TryGetProperty("final", out JsonElement final)
                    || !item.TryGetProperty("name", out JsonElement name)
                    || !item.TryGetProperty("id", out JsonElement id))
                {
                    continue;
                }

                decimal current = final.GetInt64() / 100m;
                decimal original = price.TryGetProperty("initial", out JsonElement initial) ? initial.GetInt64() / 100m : current;
                string currency = price.TryGetProperty("currency", out JsonElement cur) ? cur.GetString() ?? store.Currency : store.Currency;
                offers.Add(new StoreOffer(store.Name, name.GetString() ?? string.Empty, current, original, $"{store.BaseUrl}app/{id.GetInt64()}", currency, SteamImages.Header(id.GetInt64())));
            }

            return offers;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new ParseException("Steam response is not valid JSON.", exception);
        }
    }
}
