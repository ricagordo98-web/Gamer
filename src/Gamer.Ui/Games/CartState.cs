using Gamer.Contracts.Deals;
using Gamer.Ui.Services;

namespace Gamer.Ui.Games;

public class CartState(IGamerApi api, ToastService toasts)
{
    private List<CartItemDto> _items = [];

    public event Action? Changed;

    public IReadOnlyList<CartItemDto> Items => _items;

    public int AlertCount => _items.Count(CartAlerts.NeedsAttention);

    public CartItemDto? Find(string kind, string url) => _items.FirstOrDefault(i => i.Kind == kind && i.Url == url);

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        _items = (await api.GetCartAsync(cancellationToken)).ToList();
        Changed?.Invoke();
    }

    public async Task<bool> SaveAsync(CartItemRequest request)
    {
        try
        {
            CartItemDto saved = await api.SaveCartItemAsync(request, CancellationToken.None);
            _items = [saved, .. _items.Where(i => i.Id != saved.Id)];
            Changed?.Invoke();
            return true;
        }
        catch (ApiException exception)
        {
            toasts.Error(exception.Message);
            return false;
        }
    }

    public async Task RemoveAsync(int id)
    {
        try
        {
            await api.DeleteCartItemAsync(id, CancellationToken.None);
            _items = _items.Where(i => i.Id != id).ToList();
            Changed?.Invoke();
        }
        catch (ApiException exception)
        {
            toasts.Error(exception.Message);
        }
    }

    public void Reset()
    {
        _items = [];
        Changed?.Invoke();
    }
}
