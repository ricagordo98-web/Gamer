using System.Collections.Concurrent;

namespace Gamer.Api.Features.Deals;

public class PromotionStatusBoard
{
    private readonly ConcurrentDictionary<string, string?> _errors = new();

    public void Set(string store, string? error) => _errors[store] = error;

    public string? ErrorFor(string store) => _errors.GetValueOrDefault(store);
}
