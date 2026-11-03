using Gamer.Api.Features.Deals.Parsers;

namespace Gamer.Api.Features.Deals;

public static class ScraperCatalog
{
    public const string Steam = "Steam";
    public const string HumbleBundle = "Humble Bundle";
    public const string Fanatical = "Fanatical";
    public const string Gamesplanet = "Gamesplanet";
    public const string GreenManGaming = "Green Man Gaming";
    public const string InstantGaming = "Instant Gaming";

    public static IReadOnlyList<IStoreScraper> Create(IPageFetcher http, IPageFetcher browser)
    {
        IReadOnlyList<IPageFetcher> chain = [http, browser];

        return
        [
            new FetchingScraper(Steam,
                (store, query) => $"{store.BaseUrl}api/storesearch/?term={Uri.EscapeDataString(query)}&l=english&cc={store.Region}",
                new SteamSearchParser(), chain),
            new FetchingScraper(HumbleBundle,
                (store, query) => $"{store.BaseUrl}store/api/search?search={Uri.EscapeDataString(query)}&request=1&page_size=20&sort=bestselling&page=0&filter=all",
                new HumbleSearchParser(), chain),
            new FetchingScraper(Fanatical,
                (store, query) => $"{store.BaseUrl}en/search?search={Uri.EscapeDataString(query)}",
                new HtmlOfferParser(new HtmlSelectors(".hit-card", ".hit-card-name, .card-name", ".price-container .price, .card-price", ".was-price, .full-price", "a")),
                chain),
            new FetchingScraper(Gamesplanet,
                (store, query) => $"{store.BaseUrl}search?query={Uri.EscapeDataString(query)}",
                new HtmlOfferParser(new HtmlSelectors(".game_list .game, .game_list_item", ".name a, h4 a, .name", ".price_current, .price", ".price_base", ".name a, h4 a, a")),
                chain),
            new FetchingScraper(GreenManGaming,
                (store, query) => $"{store.BaseUrl}search/{Uri.EscapeDataString(query)}",
                new HtmlOfferParser(new HtmlSelectors(".ais-Hits-item, .product-tile", ".product-name, .game-title, h3", ".current-price, .prices .price", ".prev-price, .original-price", "a")),
                chain),
            new FetchingScraper(InstantGaming,
                (store, query) => $"{store.BaseUrl}en/search/?q={Uri.EscapeDataString(query)}",
                new HtmlOfferParser(new HtmlSelectors(".item", ".name", ".price", ".retail, .discount-old", "a.cover, a")),
                chain),
        ];
    }
}
