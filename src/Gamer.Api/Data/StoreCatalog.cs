using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;

namespace Gamer.Api.Data;

public static class StoreCatalog
{
    public static readonly IReadOnlyList<Store> All =
    [
        Create(1, ScraperCatalog.Steam, "https://store.steampowered.com/"),
        Create(2, ScraperCatalog.HumbleBundle, "https://www.humblebundle.com/"),
        Create(3, ScraperCatalog.Fanatical, "https://www.fanatical.com/"),
        Create(4, ScraperCatalog.Gamesplanet, "https://de.gamesplanet.com/"),
        Create(5, ScraperCatalog.GreenManGaming, "https://www.greenmangaming.com/"),
        Create(6, ScraperCatalog.InstantGaming, "https://www.instant-gaming.com/"),
    ];

    private static Store Create(int id, string name, string baseUrl) =>
        new() { Id = id, Name = name, BaseUrl = baseUrl, IsEnabled = true, Region = "PT", Currency = "EUR" };
}
