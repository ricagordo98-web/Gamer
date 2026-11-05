using Microsoft.JSInterop;

namespace Gamer.Ui.Services;

public record ThemeOption(string Id, string Name);

public class ThemeService(IKeyValueStore store, IJSRuntime js)
{
    public const string DefaultTheme = "modern";

    public static readonly IReadOnlyList<ThemeOption> Themes =
    [
        new(DefaultTheme, "Moderno"),
        new("neon", "Neon"),
        new("ember", "Brasa"),
        new("ocean", "Oceano"),
        new("light", "Claro"),
    ];

    public string Current { get; private set; } = DefaultTheme;

    public async Task ApplySavedAsync()
    {
        string? saved = await store.GetAsync(StorageKeys.Theme);
        await ApplyAsync(Themes.Any(t => t.Id == saved) ? saved! : DefaultTheme);
    }

    public async Task SetAsync(string theme)
    {
        await ApplyAsync(theme);
        await store.SetAsync(StorageKeys.Theme, theme);
    }

    private async Task ApplyAsync(string theme)
    {
        Current = theme;
        await js.InvokeVoidAsync("document.documentElement.setAttribute", "data-theme", theme);
    }
}
