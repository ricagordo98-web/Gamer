namespace Gamer.Contracts.Friends;

public static class FriendStates
{
    public const string Playing = "playing";
    public const string Online = "online";
    public const string Away = "away";
    public const string Offline = "offline";
}

public record FriendDto(
    string SteamId,
    string Name,
    string? AvatarUrl,
    string? ProfileUrl,
    string State,
    long? GameId,
    string? GameName,
    DateTimeOffset? LastLogoffAt,
    bool OwnsGame);

public record FriendsDto(IReadOnlyList<FriendDto> Friends, DateTimeOffset UpdatedAt);
