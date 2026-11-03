using Gamer.Contracts.Games;
using Gamer.Ui.Services;

namespace Gamer.Ui.Games;

public class GameLibrary(IGamerApi api, ToastService toasts)
{
    private List<GameDto> _games = [];

    public IReadOnlyList<GameDto> Games => _games;
    public IReadOnlyList<PlatformDto> Platforms { get; private set; } = [];
    public GameFilter Filter { get; } = new();
    public bool IsLoaded { get; private set; }
    public bool IsLoading { get; private set; }
    public string? LoadError { get; private set; }

    public event Action? Changed;

    public void Reset()
    {
        _games = [];
        Platforms = [];
        IsLoaded = false;
        LoadError = null;
        Filter.Reset();
        Changed?.Invoke();
    }

        public async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (!IsLoaded && !IsLoading)
        {
            await ReloadAsync(cancellationToken);
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        LoadError = null;
        Changed?.Invoke();

        try
        {
            Task<IReadOnlyList<GameDto>> games = api.GetGamesAsync(cancellationToken);
            Task<IReadOnlyList<PlatformDto>> platforms = api.GetPlatformsAsync(cancellationToken);
            await Task.WhenAll(games, platforms);

            _games = [.. games.Result];
            SortGames();
            Platforms = platforms.Result;
            IsLoaded = true;
        }
        catch (ApiException exception)
        {
            LoadError = exception.Message;
        }
        finally
        {
            IsLoading = false;
            Changed?.Invoke();
        }
    }

    public async Task<bool> SaveAsync(int? id, GameRequest request, CancellationToken cancellationToken)
    {
        return await RunAsync(async () =>
        {
            if (id is null)
            {
                _games.Add(await api.CreateGameAsync(request, cancellationToken));
                toasts.Success("Jogo adicionado.");
            }
            else
            {
                Replace(await api.UpdateGameAsync(id.Value, request, cancellationToken));
                toasts.Success("Jogo atualizado.");
            }

            SortGames();
        });
    }

    public Task<bool> SetStatusAsync(GameDto game, GameStatus status, CancellationToken cancellationToken) =>
        RunAsync(async () => Replace(await api.ChangeStatusAsync(game.Id, status, cancellationToken)));

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            await api.DeleteGameAsync(id, cancellationToken);
            _games.RemoveAll(g => g.Id == id);
            toasts.Success("Jogo apagado.");
        });

    private async Task<bool> RunAsync(Func<Task> action)
    {
        try
        {
            await action();
            Changed?.Invoke();
            return true;
        }
        catch (ApiException exception)
        {
            toasts.Error(exception.Message);
            return false;
        }
    }

    private void Replace(GameDto updated)
    {
        int index = _games.FindIndex(g => g.Id == updated.Id);
        if (index >= 0)
        {
            _games[index] = updated;
        }
    }

    private void SortGames() => _games.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
}

