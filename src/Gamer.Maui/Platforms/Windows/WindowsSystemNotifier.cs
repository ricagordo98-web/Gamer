using System.Runtime.InteropServices;
using Gamer.Ui.Services;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace Gamer.Maui.WinUI;

public sealed class WindowsSystemNotifier : ISystemNotifier
{
    private readonly bool _registered;

    public WindowsSystemNotifier()
    {
        try
        {
            AppNotificationManager.Default.NotificationInvoked += (_, _) => { };
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (COMException)
        {
            _registered = false;
        }
    }

    public void Notify(string title, string message)
    {
        if (!_registered)
        {
            return;
        }

        try
        {
            AppNotificationManager.Default.Show(new AppNotificationBuilder().AddText(title).AddText(message).BuildNotification());
        }
        catch (COMException)
        {
        }
    }
}
