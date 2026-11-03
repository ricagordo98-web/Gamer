namespace Gamer.Ui.Services;

public enum ToastKind
{
    Info,
    Success,
    Error,
}

public record Toast(Guid Id, string Message, ToastKind Kind);

public class ToastService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(3.5);
    private readonly List<Toast> _toasts = [];

    public IReadOnlyList<Toast> Toasts => _toasts;

    public event Action? Changed;

    public void Success(string message) => Show(message, ToastKind.Success);

    public void Error(string message) => Show(message, ToastKind.Error);

    private void Show(string message, ToastKind kind)
    {
        Toast toast = new(Guid.NewGuid(), message, kind);
        _toasts.Add(toast);
        Changed?.Invoke();
        _ = DismissAfterLifetimeAsync(toast);
    }

    private async Task DismissAfterLifetimeAsync(Toast toast)
    {
        await Task.Delay(Lifetime);
        _toasts.Remove(toast);
        Changed?.Invoke();
    }
}
