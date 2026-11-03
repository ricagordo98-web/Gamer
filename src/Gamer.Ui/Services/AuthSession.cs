using System.Globalization;
using Gamer.Contracts.Auth;

namespace Gamer.Ui.Services;

public class AuthSession(IKeyValueStore store, TimeProvider clock)
{
    private bool _initialized;
    private DateTimeOffset _expiresAt;

    public string? Token { get; private set; }

    public bool IsAuthenticated => Token is not null && _expiresAt > clock.GetUtcNow();

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        string? token = await store.GetAsync(StorageKeys.Token);
        string? expiry = await store.GetAsync(StorageKeys.TokenExpiry);
        if (token is not null && DateTimeOffset.TryParse(expiry, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset expiresAt))
        {
            Token = token;
            _expiresAt = expiresAt;
        }
    }

    public async Task SignInAsync(LoginResponse login)
    {
        Token = login.Token;
        _expiresAt = login.ExpiresAt;
        await store.SetAsync(StorageKeys.Token, login.Token);
        await store.SetAsync(StorageKeys.TokenExpiry, login.ExpiresAt.ToString("O", CultureInfo.InvariantCulture));
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        Token = null;
        await store.RemoveAsync(StorageKeys.Token);
        await store.RemoveAsync(StorageKeys.TokenExpiry);
        Changed?.Invoke();
    }
}
