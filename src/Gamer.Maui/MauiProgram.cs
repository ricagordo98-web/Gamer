using Gamer.Ui.Services;
using Microsoft.Extensions.Logging;

namespace Gamer.Maui;

public static class MauiProgram
{
    private const string DesktopServerUrl = "http://localhost:5199";
    private const string AndroidEmulatorServerUrl = "http://10.0.2.2:5199";

    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<IKeyValueStore, PreferencesKeyValueStore>();
        builder.Services.AddGamerUi(DeviceInfo.Platform == DevicePlatform.Android ? AndroidEmulatorServerUrl : DesktopServerUrl);

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
