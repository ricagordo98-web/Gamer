namespace Gamer.Contracts.Steam;

public record SteamSettingsRequest(string? ApiKey, string SteamId, int RefreshHours);
