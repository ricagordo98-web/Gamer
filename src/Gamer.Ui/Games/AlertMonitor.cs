using System.Text.Json;
using Gamer.Contracts.Deals;
using Gamer.Ui.Services;

namespace Gamer.Ui.Games;

public class AlertMonitor(CartState cart, AuthSession session, ToastService toasts, ISystemNotifier notifier, IKeyValueStore store, TimeProvider time)
{
    private const int MaxRemembered = 300;

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private bool _started;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        while (true)
        {
            if (session.IsAuthenticated)
            {
                try
                {
                    await cart.LoadAsync(CancellationToken.None);
                    await NotifyNewAlertsAsync();
                }
                catch (ApiException)
                {
                }
            }

            await Task.Delay(Interval);
        }
    }

    private async Task NotifyNewAlertsAsync()
    {
        List<string> notified = await LoadNotifiedAsync();
        DateTimeOffset now = time.GetUtcNow();
        List<CartAlert> fresh = cart.Items
            .SelectMany(item => CartAlerts.All(item, now))
            .OfType<CartAlert>()
            .Where(alert => !notified.Contains(alert.Key))
            .ToList();

        foreach (CartAlert alert in fresh)
        {
            if (alert.Good)
            {
                toasts.Success($"{alert.Title} {alert.Message}");
            }
            else
            {
                toasts.Error($"{alert.Title}: {alert.Message}");
            }

            notifier.Notify(alert.Title, alert.Message);
            notified.Add(alert.Key);
        }

        if (fresh.Count > 0)
        {
            await store.SetAsync(StorageKeys.NotifiedAlerts, JsonSerializer.Serialize(notified.TakeLast(MaxRemembered)));
        }
    }

    private async Task<List<string>> LoadNotifiedAsync()
    {
        string? json = await store.GetAsync(StorageKeys.NotifiedAlerts);
        try
        {
            return json is null ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
