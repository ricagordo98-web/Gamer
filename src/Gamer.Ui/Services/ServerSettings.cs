namespace Gamer.Ui.Services;

public class ServerSettings(IKeyValueStore store, GamerUiOptions options)
{
    private string? _baseUrl;

    public async ValueTask<string> GetBaseUrlAsync()
    {
        _baseUrl ??= Normalize(await store.GetAsync(StorageKeys.ServerUrl))
            ?? Normalize(options.DefaultServerUrl)
            ?? throw new InvalidOperationException("No server URL configured.");
        return _baseUrl;
    }

    public async ValueTask<bool> TrySetBaseUrlAsync(string? value)
    {
        string? normalized = Normalize(value);
        if (normalized is null)
        {
            return false;
        }

        _baseUrl = normalized;
        await store.SetAsync(StorageKeys.ServerUrl, normalized);
        return true;
    }

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string text = value.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = $"http://{text}";
        }

        bool valid = Uri.TryCreate(text, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        return valid ? uri!.GetLeftPart(UriPartial.Authority) + "/" : null;
    }
}
