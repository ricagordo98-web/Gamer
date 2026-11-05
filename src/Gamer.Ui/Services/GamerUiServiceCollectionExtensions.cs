using Gamer.Ui.Games;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gamer.Ui.Services;

public static class GamerUiServiceCollectionExtensions
{
    private const string PlaceholderBaseAddress = "http://gamer.invalid/";

    public static IServiceCollection AddGamerUi(this IServiceCollection services, string? defaultServerUrl)
    {
        services.AddSingleton(new GamerUiOptions(defaultServerUrl));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ServerSettings>();
        services.AddSingleton<AuthSession>();
        services.AddSingleton<ToastService>();
        services.AddSingleton<GameLibrary>();
        services.AddScoped<ThemeService>();
        services.AddSingleton<CartState>();
        services.AddSingleton<AlertMonitor>();
        services.TryAddSingleton<ISystemNotifier, NoSystemNotifier>();
        services.AddTransient<ApiHandler>();

        services.AddHttpClient<IGamerApi, GamerApiClient>(client => client.BaseAddress = new Uri(PlaceholderBaseAddress))
            .AddHttpMessageHandler<ApiHandler>();

        return services;
    }
}
