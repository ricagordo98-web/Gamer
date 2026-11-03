using Gamer.Ui.Services;

namespace Gamer.Maui;

public class PreferencesKeyValueStore : IKeyValueStore
{
    public ValueTask<string?> GetAsync(string key) =>
        ValueTask.FromResult(Preferences.Default.ContainsKey(key) ? Preferences.Default.Get<string?>(key, null) : null);

    public ValueTask SetAsync(string key, string value)
    {
        Preferences.Default.Set(key, value);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string key)
    {
        Preferences.Default.Remove(key);
        return ValueTask.CompletedTask;
    }
}
