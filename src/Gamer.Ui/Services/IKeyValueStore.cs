namespace Gamer.Ui.Services;

public interface IKeyValueStore
{
    ValueTask<string?> GetAsync(string key);
    ValueTask SetAsync(string key, string value);
    ValueTask RemoveAsync(string key);
}
