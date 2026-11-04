using System.Text.Json;
using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Parsers;

public class SteamSpecialsParser : IOfferParser
{
    private static readonly string[] Sections = ["specials", "top_sellers"];

    public IReadOnlyList<StoreOffer> Parse(string content, Store store)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(JsonText.Extract(content));
            Dictionary<long, StoreOffer> offers = [];
            bool foundSection = false;

            foreach (string section in Sections)
            {
                if (!document.RootElement.TryGetProperty(section, out JsonElement block) || !block.TryGetProperty("items", out JsonElement items))
                {
                    continue;
                }

                foundSection = true;
                foreach (JsonElement item in items.EnumerateArray())
                {
                    if (TryRead(item, store, out long id, out StoreOffer? offer))
                    {
                        offers.TryAdd(id, offer);
                    }
                }
            }

            return foundSection ? offers.Values.ToList() : throw new ParseException("Steam response has no deal sections.");
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new ParseException("Steam response is not valid JSON.", exception);
        }
    }

    private static bool TryRead(JsonElement item, Store store, out long id, out StoreOffer offer)
    {
        id = 0;
        offer = null!;
        if (!item.TryGetProperty("id", out JsonElement idElement)
            || !item.TryGetProperty("name", out JsonElement name)
            || !item.TryGetProperty("final_price", out JsonElement final)
            || !item.TryGetProperty("original_price", out JsonElement original)
            || !item.TryGetProperty("discounted", out JsonElement discounted)
            || !discounted.GetBoolean())
        {
            return false;
        }

        id = idElement.GetInt64();
        string currency = item.TryGetProperty("currency", out JsonElement cur) ? cur.GetString() ?? store.Currency : store.Currency;
        offer = new StoreOffer(store.Name, name.GetString() ?? string.Empty, final.GetInt64() / 100m, original.GetInt64() / 100m, $"{store.BaseUrl}app/{id}", currency, SteamImages.Header(id));
        return true;
    }
}
