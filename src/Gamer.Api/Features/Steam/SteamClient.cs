using System.Text.Json;

namespace Gamer.Api.Features.Steam;

public record SteamOwnedGame(long AppId, string Name, int PlaytimeMinutes);

public record SteamWishlistEntry(long AppId, DateTimeOffset? AddedAt);

public class SteamException(string message, Exception? inner = null) : Exception(message, inner);

public class SteamNotConfiguredException() : SteamException("Configura a API key e o SteamID64 nas Definições.");

public interface ISteamClient
{
    Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string apiKey, string steamId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SteamWishlistEntry>> GetWishlistAsync(string apiKey, string steamId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<long, string>> GetAppNamesAsync(IReadOnlyCollection<long> appIds, CancellationToken cancellationToken);
}

public class SteamClient(HttpClient http) : ISteamClient
{
    private const int NameBatchSize = 100;

    public async Task<IReadOnlyList<SteamOwnedGame>> GetOwnedGamesAsync(string apiKey, string steamId, CancellationToken cancellationToken)
    {
        string url = $"IPlayerService/GetOwnedGames/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId)}&include_appinfo=true&format=json";
        using JsonDocument document = await GetJsonAsync(url, cancellationToken);

        if (!document.RootElement.TryGetProperty("response", out JsonElement response)
            || !response.TryGetProperty("games", out JsonElement games))
        {
            throw new SteamException("A biblioteca Steam está vazia ou o perfil não é público.");
        }

        return games.EnumerateArray()
            .Where(g => g.TryGetProperty("name", out _))
            .Select(g => new SteamOwnedGame(
                g.GetProperty("appid").GetInt64(),
                g.GetProperty("name").GetString() ?? string.Empty,
                g.TryGetProperty("playtime_forever", out JsonElement playtime) ? playtime.GetInt32() : 0))
            .ToList();
    }

    public async Task<IReadOnlyList<SteamWishlistEntry>> GetWishlistAsync(string apiKey, string steamId, CancellationToken cancellationToken)
    {
        string url = $"IWishlistService/GetWishlist/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId)}";
        using JsonDocument document = await GetJsonAsync(url, cancellationToken);

        if (!document.RootElement.TryGetProperty("response", out JsonElement response)
            || !response.TryGetProperty("items", out JsonElement items))
        {
            return [];
        }

        return items.EnumerateArray()
            .Select(i => new SteamWishlistEntry(
                i.GetProperty("appid").GetInt64(),
                i.TryGetProperty("date_added", out JsonElement added) ? DateTimeOffset.FromUnixTimeSeconds(added.GetInt64()) : null))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<long, string>> GetAppNamesAsync(IReadOnlyCollection<long> appIds, CancellationToken cancellationToken)
    {
        Dictionary<long, string> names = [];
        foreach (long[] batch in appIds.Chunk(NameBatchSize))
        {
            string input = JsonSerializer.Serialize(new
            {
                ids = batch.Select(id => new { appid = id }),
                context = new { language = "english", country_code = "PT" },
                data_request = new { },
            });
            using JsonDocument document = await GetJsonAsync($"IStoreBrowseService/GetItems/v1/?input_json={Uri.EscapeDataString(input)}", cancellationToken);

            if (!document.RootElement.TryGetProperty("response", out JsonElement response)
                || !response.TryGetProperty("store_items", out JsonElement items))
            {
                continue;
            }

            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.TryGetProperty("appid", out JsonElement id) && item.TryGetProperty("name", out JsonElement name))
                {
                    names[id.GetInt64()] = name.GetString() ?? string.Empty;
                }
            }
        }

        return names;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new SteamException("A Steam recusou a API key ou o perfil não é público.");
            }

            response.EnsureSuccessStatusCode();
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new SteamException("Não foi possível obter dados da Steam.", exception);
        }
    }
}
