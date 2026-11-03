namespace Gamer.Contracts.Steam;

public record SteamSettingsDto(bool ApiKeyConfigured, string SteamId, int RefreshHours);
