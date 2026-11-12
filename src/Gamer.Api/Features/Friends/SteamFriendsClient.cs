using System.Text.Json;
using Gamer.Api.Features.Steam;

namespace Gamer.Api.Features.Friends;

public record SteamPlayer(string SteamId, string Name, string? AvatarUrl, string? ProfileUrl, int PersonaState, long? GameId, string? GameName, DateTimeOffset? LastLogoffAt);

public interface ISteamFriendsClient
{
    Task<IReadOnlyList<string>> GetFriendIdsAsync(string apiKey, string steamId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SteamPlayer>> GetPlayersAsync(string apiKey, IReadOnlyCollection<string> steamIds, CancellationToken cancellationToken);
}

public class SteamFriendsClient(HttpClient http) : ISteamFriendsClient
{
    private const int SummaryBatchSize = 100;

    public async Task<IReadOnlyList<string>> GetFriendIdsAsync(string apiKey, string steamId, CancellationToken cancellationToken)
    {
        using JsonDocument document = await GetJsonAsync(
            $"ISteamUser/GetFriendList/v1/?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId)}&relationship=friend", cancellationToken);
        return ParseFriendIds(document.RootElement);
    }

    public async Task<IReadOnlyList<SteamPlayer>> GetPlayersAsync(string apiKey, IReadOnlyCollection<string> steamIds, CancellationToken cancellationToken)
    {
        List<SteamPlayer> players = [];
        foreach (string[] batch in steamIds.Chunk(SummaryBatchSize))
        {
            using JsonDocument document = await GetJsonAsync(
                $"ISteamUser/GetPlayerSummaries/v2/?key={Uri.EscapeDataString(apiKey)}&steamids={string.Join(',', batch)}", cancellationToken);
            players.AddRange(ParsePlayers(document.RootElement));
        }

        return players;
    }

    public static IReadOnlyList<string> ParseFriendIds(JsonElement root) =>
        root.TryGetProperty("friendslist", out JsonElement list) && list.TryGetProperty("friends", out JsonElement friends)
            ? friends.EnumerateArray().Select(f => f.GetProperty("steamid").GetString() ?? string.Empty).Where(id => id.Length > 0).ToList()
            : [];

    public static IReadOnlyList<SteamPlayer> ParsePlayers(JsonElement root)
    {
        if (!root.TryGetProperty("response", out JsonElement response) || !response.TryGetProperty("players", out JsonElement players))
        {
            return [];
        }

        return players.EnumerateArray().Select(p => new SteamPlayer(
            p.GetProperty("steamid").GetString() ?? string.Empty,
            p.TryGetProperty("personaname", out JsonElement name) ? name.GetString() ?? string.Empty : string.Empty,
            p.TryGetProperty("avatarfull", out JsonElement avatar) ? avatar.GetString() : null,
            p.TryGetProperty("profileurl", out JsonElement profile) ? profile.GetString() : null,
            p.TryGetProperty("personastate", out JsonElement state) ? state.GetInt32() : 0,
            p.TryGetProperty("gameid", out JsonElement gameId) && long.TryParse(gameId.GetString(), out long id) ? id : null,
            p.TryGetProperty("gameextrainfo", out JsonElement gameName) ? gameName.GetString() : null,
            p.TryGetProperty("lastlogoff", out JsonElement lastLogoff) && lastLogoff.GetInt64() > 0 ? DateTimeOffset.FromUnixTimeSeconds(lastLogoff.GetInt64()) : null)).ToList();
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new SteamException("A Steam recusou o pedido: a lista de amigos tem de estar pública e a API key válida.");
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
