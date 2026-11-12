using System.Net.Http.Json;
using System.Text.Json;

namespace Gamer.Api.Features.Stats;

public record HistoricLow(decimal Price, string? Shop);

public interface IHistoricLowClient
{
    Task<HistoricLow?> GetAsync(string apiKey, long appId, CancellationToken cancellationToken);
}

public class HistoricLowClient(HttpClient http) : IHistoricLowClient
{
    private const int SteamShopId = 61;
    private const string Country = "PT";

    public async Task<HistoricLow?> GetAsync(string apiKey, long appId, CancellationToken cancellationToken)
    {
        try
        {
            string key = Uri.EscapeDataString(apiKey);
            string appKey = $"app/{appId}";

            using HttpResponseMessage lookup = await http.PostAsJsonAsync($"lookup/id/shop/{SteamShopId}/v1?key={key}", new[] { appKey }, cancellationToken);
            if (!lookup.IsSuccessStatusCode)
            {
                return null;
            }

            using JsonDocument ids = await JsonDocument.ParseAsync(await lookup.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            if (!ids.RootElement.TryGetProperty(appKey, out JsonElement idElement) || idElement.GetString() is not { } gameId)
            {
                return null;
            }

            using HttpResponseMessage history = await http.PostAsJsonAsync($"games/historylow/v1?key={key}&country={Country}", new[] { gameId }, cancellationToken);
            if (!history.IsSuccessStatusCode)
            {
                return null;
            }

            using JsonDocument document = await JsonDocument.ParseAsync(await history.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return Parse(document.RootElement);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public static HistoricLow? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0
            || !root[0].TryGetProperty("low", out JsonElement low) || low.ValueKind != JsonValueKind.Object
            || !low.TryGetProperty("price", out JsonElement price) || !price.TryGetProperty("amount", out JsonElement amount))
        {
            return null;
        }

        string? shop = low.TryGetProperty("shop", out JsonElement shopElement) && shopElement.TryGetProperty("name", out JsonElement name)
            ? name.GetString()
            : null;

        return new HistoricLow(amount.GetDecimal(), shop);
    }
}
