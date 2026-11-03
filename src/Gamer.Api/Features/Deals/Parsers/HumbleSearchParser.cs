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
            if (!document.RootElement.TryGetProperty("results", out JsonElement results))
            {
                throw new ParseException("Humble response has no results.");
            }

            List<StoreOffer> offers = [];
            foreach (JsonElement item in results.EnumerateArray())
            {
                if (!item.TryGetProperty("current_price", out JsonElement current)
                    || !item.TryGetProperty("human_name", out JsonElement name)
                    || !item.TryGetProperty("machine_name", out JsonElement machine))
                {
                    continue;
                }

                decimal price = current[0].GetDecimal();
                string currency = current[1].GetString() ?? store.Currency;
                decimal original = item.TryGetProperty("full_price", out JsonElement full) ? full[0].GetDecimal() : price;
                offers.Add(new StoreOffer(store.Name, name.GetString() ?? string.Empty, price, original, $"{store.BaseUrl}store/{machine.GetString()}", currency));
            }

            return offers;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            throw new ParseException("Humble response is not valid JSON.", exception);
        }
    }
}
