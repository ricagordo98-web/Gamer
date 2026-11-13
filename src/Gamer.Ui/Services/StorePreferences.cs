namespace Gamer.Ui.Services;

public class StorePreferences(IKeyValueStore store)
{
    private bool _loaded;

    public bool HideOwned { get; private set; }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        HideOwned = await store.GetAsync(StorageKeys.HideOwned) == bool.TrueString;
        _loaded = true;
    }

    public async Task SetHideOwnedAsync(bool value)
    {
        HideOwned = value;
        _loaded = true;
        await store.SetAsync(StorageKeys.HideOwned, value.ToString());
    }
}
