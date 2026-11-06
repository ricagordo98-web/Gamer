using System.Text.Json;

namespace Gamer.Api.Features.Library;

public static class EpicManifestParser
{
    /// <summary>Reads the game name from an Epic launcher ".item" manifest; null for DLC and add-ons.</summary>
    public static string? ParseTitle(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string? name = root.TryGetProperty("DisplayName", out JsonElement display) ? display.GetString() : null;
            string? app = root.TryGetProperty("AppName", out JsonElement appName) ? appName.GetString() : null;
            string? main = root.TryGetProperty("MainGameAppName", out JsonElement mainName) ? mainName.GetString() : null;

            bool isAddOn = !string.IsNullOrEmpty(main) && !string.Equals(main, app, StringComparison.OrdinalIgnoreCase);
            return string.IsNullOrWhiteSpace(name) || isAddOn ? null : name.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
