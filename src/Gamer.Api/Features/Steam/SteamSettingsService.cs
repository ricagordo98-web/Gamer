using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Steam;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Steam;

public record SteamCredentials(string ApiKey, string SteamId);

public interface ISteamSettingsService
{
    Task<SteamSettingsDto> GetAsync(CancellationToken cancellationToken);
    Task SaveAsync(SteamSettingsRequest request, CancellationToken cancellationToken);
    Task<SteamCredentials?> GetCredentialsAsync(CancellationToken cancellationToken);
    Task<int> GetRefreshHoursAsync(CancellationToken cancellationToken);
}

public class SteamSettingsService(GamerDbContext db) : ISteamSettingsService
{
    public const int DefaultRefreshHours = 12;

    private const string ApiKeySetting = "steam.apiKey";
    private const string SteamIdSetting = "steam.steamId";
    private const string RefreshHoursSetting = "steam.refreshHours";

    public async Task<SteamSettingsDto> GetAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, string> values = await LoadAsync(cancellationToken);
        return new SteamSettingsDto(
            values.ContainsKey(ApiKeySetting),
            values.GetValueOrDefault(SteamIdSetting, string.Empty),
            ParseHours(values));
    }

    public async Task SaveAsync(SteamSettingsRequest request, CancellationToken cancellationToken)
    {
        Dictionary<string, AppSetting> existing = await db.AppSettings.ToDictionaryAsync(s => s.Key, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            Upsert(existing, ApiKeySetting, request.ApiKey.Trim());
        }

        Upsert(existing, SteamIdSetting, request.SteamId.Trim());
        Upsert(existing, RefreshHoursSetting, request.RefreshHours.ToString());
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SteamCredentials?> GetCredentialsAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, string> values = await LoadAsync(cancellationToken);
        return values.TryGetValue(ApiKeySetting, out string? apiKey)
            && values.TryGetValue(SteamIdSetting, out string? steamId)
            && steamId.Length > 0
                ? new SteamCredentials(apiKey, steamId)
                : null;
    }

    public async Task<int> GetRefreshHoursAsync(CancellationToken cancellationToken) =>
        ParseHours(await LoadAsync(cancellationToken));

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken) =>
        await db.AppSettings.AsNoTracking()
            .Where(s => s.Value != string.Empty)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

    private static int ParseHours(Dictionary<string, string> values) =>
        values.TryGetValue(RefreshHoursSetting, out string? text) && int.TryParse(text, out int hours)
            ? hours
            : DefaultRefreshHours;

    private void Upsert(Dictionary<string, AppSetting> existing, string key, string value)
    {
        if (existing.TryGetValue(key, out AppSetting? setting))
        {
            setting.Value = value;
        }
        else
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
    }
}
