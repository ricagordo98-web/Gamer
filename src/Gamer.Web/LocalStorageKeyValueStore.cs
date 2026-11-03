using Gamer.Ui.Services;
using Microsoft.JSInterop;

namespace Gamer.Web;

public class LocalStorageKeyValueStore(IJSRuntime js) : IKeyValueStore
{
    public ValueTask<string?> GetAsync(string key) => js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) => js.InvokeVoidAsync("localStorage.setItem", key, value);

    public ValueTask RemoveAsync(string key) => js.InvokeVoidAsync("localStorage.removeItem", key);
}
