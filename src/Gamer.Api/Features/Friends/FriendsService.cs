using Gamer.Api.Data;
using Gamer.Api.Features.Steam;
using Gamer.Contracts.Friends;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Friends;

public sealed class FriendsCache
{
    public IReadOnlyList<SteamPlayer>? Players { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

public class FriendsService(GamerDbContext db, ISteamFriendsClient client, ISteamSettingsService settings, FriendsCache cache, TimeProvider clock)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    public async Task<FriendsDto> GetAsync(bool refresh, CancellationToken cancellationToken)
    {
        IReadOnlyList<SteamPlayer> players;
        DateTimeOffset updatedAt;
        await cache.Gate.WaitAsync(cancellationToken);
        try
        {
            DateTimeOffset now = clock.GetUtcNow();
            if (refresh || cache.Players is null || now - cache.UpdatedAt >= Lifetime)
            {
                SteamCredentials credentials = await settings.GetCredentialsAsync(cancellationToken)
                    ?? throw new SteamNotConfiguredException();
                IReadOnlyList<string> ids = await client.GetFriendIdsAsync(credentials.ApiKey, credentials.SteamId, cancellationToken);
                cache.Players = ids.Count == 0 ? [] : await client.GetPlayersAsync(credentials.ApiKey, ids, cancellationToken);
                cache.UpdatedAt = now;
            }

            players = cache.Players;
            updatedAt = cache.UpdatedAt;
        }
        finally
        {
            cache.Gate.Release();
        }

        HashSet<long> owned = (await db.SteamLibrary.AsNoTracking().Select(i => i.AppId).ToListAsync(cancellationToken)).ToHashSet();
        List<FriendDto> friends = players
            .Select(p => ToDto(p, owned))
            .OrderBy(f => StateOrder(f.State))
            .ThenBy(f => f.GameName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(f => f.LastLogoffAt)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new FriendsDto(friends, updatedAt);
    }

    private static FriendDto ToDto(SteamPlayer player, HashSet<long> owned) => new(
        player.SteamId,
        player.Name,
        player.AvatarUrl,
        player.ProfileUrl,
        StateOf(player),
        player.GameId,
        player.GameName,
        player.LastLogoffAt,
        player.GameId is { } gameId && owned.Contains(gameId));

    private static string StateOf(SteamPlayer player) =>
        player.GameId is not null ? FriendStates.Playing
        : player.PersonaState switch
        {
            0 => FriendStates.Offline,
            3 or 4 => FriendStates.Away,
            _ => FriendStates.Online,
        };

    private static int StateOrder(string state) => state switch
    {
        FriendStates.Playing => 0,
        FriendStates.Online => 1,
        FriendStates.Away => 2,
        _ => 3,
    };
}
