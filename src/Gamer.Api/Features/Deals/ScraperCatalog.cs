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

    private const string CoverPrefix = "Product cover for ";

    private static readonly string[] GamesplanetCategories = ["action", "arcade_indie", "strategy", "rpg", "simulation", "adventure"];

    public static IReadOnlyList<IStoreScraper> Create(IPageFetcher http, IPageFetcher browser)
    {
        IReadOnlyList<IPageFetcher> httpFirst = [http, browser];
        IReadOnlyList<IPageFetcher> browserOnly = [browser];

        IOfferParser steamSearch = new SteamSearchParser();
        HtmlOfferParser humble = new(new HtmlSelectors(
            ".entity-block-container", ".entity-title", ".price-button .price", ".breakdown-full-price",
            "a.entity-link", "img.entity-image", ".discount-amount"));
        HtmlOfferParser fanatical = new(
            new HtmlSelectors(
                ".HitCardContainer", "img[alt]", ".card-price", ".was-price",
                "a.hitCardStripe__content__purchase__price", ".product-cover-container img", ".card-saving", ".CountdownTimer"),
            name => name.StartsWith(CoverPrefix, StringComparison.OrdinalIgnoreCase) ? name[CoverPrefix.Length..] : name);
        HtmlOfferParser gamesplanet = new(new HtmlSelectors(
            ".game_list, .game_list_small", "h4 a", ".price_current", ".price_base", "h4 a", "img", ".price_saving"));
        HtmlOfferParser instantGaming = new(new HtmlSelectors(
            ".item", ".title", ".price", ".retail-price", "a.cover", "a.cover img", ".discount"));
        IOfferParser greenManGaming = new GreenManGamingParser();

        return
        [
            new FetchingScraper(Steam,
                new ScraperPages(
                    (store, query) => $"{store.BaseUrl}api/storesearch/?term={Uri.EscapeDataString(query)}&l=portuguese&cc={store.Region}",
                    store => $"{store.BaseUrl}api/featuredcategories?l=portuguese&cc={store.Region}",
                    steamSearch, new SteamSpecialsParser(), null),
                httpFirst),
            new FetchingScraper(HumbleBundle,
                new ScraperPages(
                    (store, query) => $"{store.BaseUrl}store/search?search={Uri.EscapeDataString(query)}",
                    store => $"{store.BaseUrl}store/search?sort=bestselling&filter=onsale",
                    humble, humble, ".entity-block-container",
                    (store, page) => $"{store.BaseUrl}store/search?sort=bestselling&page={page}"),
                browserOnly),
            new FetchingScraper(Fanatical,
                new ScraperPages(
                    (store, query) => $"{store.BaseUrl}en/search?search={Uri.EscapeDataString(query)}",
                    store => $"{store.BaseUrl}en/on-sale",
                    fanatical, fanatical, ".HitCardContainer",
                    (store, page) => $"{store.BaseUrl}en/search?page={page}"),
                browserOnly),
            new FetchingScraper(Gamesplanet,
                new ScraperPages(
                    (store, query) => $"{store.BaseUrl}search?query={Uri.EscapeDataString(query)}",
                    store => $"{store.BaseUrl}games/offers",
                    gamesplanet, gamesplanet, ".game_list",
                    (store, page) => $"{store.BaseUrl}games/{GamesplanetCategories[(page - 1) % GamesplanetCategories.Length]}?page={(page - 1) / GamesplanetCategories.Length + 1}"),
                httpFirst),
            new FetchingScraper(GreenManGaming,
                new ScraperPages(
                    (store, query) => $"{store.BaseUrl}search?query={Uri.EscapeDataString(query)}",
                    store => $"{store.BaseUrl}search?sort=discount&onsale=true",
                    greenManGaming, greenManGaming, GreenManGamingParser.ItemSelector,
                    (store, _) => $"{store.BaseUrl}search"),
                browserOnly),
            new FetchingScraper(InstantGaming,
                new ScraperPages(
                    (store, query) => $"{store.BaseUrl}pt/pesquisar/?q={Uri.EscapeDataString(query)}",
                    store => $"{store.BaseUrl}pt/pesquisar/?sort_by=bestsellers_desc",
                    instantGaming, instantGaming, ".item",
                    (store, page) => $"{store.BaseUrl}pt/pesquisar/?sort_by=bestsellers_desc&page={page}"),
                browserOnly),
        ];
    }
}
