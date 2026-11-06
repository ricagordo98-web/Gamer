using Gamer.Contracts.Deals;

namespace Gamer.Ui.Games;

public record CartAlert(string Key, string Title, string Message, bool Good);

public static class CartAlerts
{
    private static readonly TimeSpan LastHours = TimeSpan.FromHours(6);

    public static bool IsAlert(string status) => status is not CartStatuses.Active;

    public static bool NeedsAttention(CartItemDto item) => IsAlert(item.Status) || item.Cheaper is not null;

    public static CartAlert? CheaperElsewhere(CartItemDto item) =>
        item.Cheaper is { } cheaper
            ? new CartAlert(
                $"{item.Id}:cheaper:{cheaper.Store}:{cheaper.Price}",
                "Mais barato noutra loja",
                $"{item.Title}: {DealFormat.Money(item.CurrentPrice ?? item.Price, item.Currency)} em {item.Store} → {DealFormat.Money(cheaper.Price, cheaper.Currency)} em {cheaper.Store}.",
                true)
            : null;

    public static IEnumerable<CartAlert> All(CartItemDto item, DateTimeOffset now) =>
        new[] { For(item, now), CheaperElsewhere(item) }.OfType<CartAlert>();

    public static CartAlert? For(CartItemDto item, DateTimeOffset now)
    {
        string name = $"{item.Title} ({item.Store})";
        return item.Status switch
        {
            CartStatuses.EndingSoon when item.EndsAt is { } end => new CartAlert(
                $"{item.Id}:ending:{(end - now <= LastHours ? "6h" : "48h")}",
                "Promoção a acabar",
                $"{name}: {DealFormat.TimeLeft(end, now)?.ToLowerInvariant() ?? "acaba em breve"}.",
                false),
            CartStatuses.Ended => new CartAlert($"{item.Id}:ended", "Promoção terminou", $"{name} já não está em promoção.", false),
            CartStatuses.PriceUp when item.CurrentPrice is { } current => new CartAlert(
                $"{item.Id}:up:{current}",
                "O preço subiu",
                $"{name}: {DealFormat.Money(item.Price, item.Currency)} → {DealFormat.Money(current, item.Currency)}.",
                false),
            CartStatuses.PriceDown when item.CurrentPrice is { } current => new CartAlert(
                $"{item.Id}:down:{current}",
                "O preço desceu!",
                $"{name}: {DealFormat.Money(item.Price, item.Currency)} → {DealFormat.Money(current, item.Currency)}.",
                true),
            _ => null,
        };
    }
}
