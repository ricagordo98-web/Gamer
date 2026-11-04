using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Parsers;

public partial class GreenManGamingParser : IOfferParser
{
    public const string ItemSelector = ".ais-Hits-item";

    public IReadOnlyList<StoreOffer> Parse(string content, Store store)
    {
        IHtmlCollection<IElement> items = new HtmlParser().ParseDocument(content).QuerySelectorAll(ItemSelector);
        if (items.Length == 0)
        {
            throw new NoResultsException("No result items found in the page.");
        }

        Uri baseUri = new(store.BaseUrl);
        List<StoreOffer> offers = [];
        foreach (IElement item in items)
        {
            IElement? holder = item.QuerySelector("[ng-init]");
            Match match = Payload().Match(holder?.GetAttribute("ng-init") ?? string.Empty);
            if (!match.Success || !TryRead(match.Groups[1].Value, baseUri, store, out StoreOffer? offer))
            {
                continue;
            }

            offers.Add(offer);
        }

        return offers;
    }

    private static bool TryRead(string payload, Uri baseUri, Store store, out StoreOffer offer)
    {
        offer = null!;
        try
        {
            using JsonDocument document = JsonDocument.Parse(Encoding.Unicode.GetString(Convert.FromBase64String(payload)));
            JsonElement root = document.RootElement;
            JsonElement edition = root.GetProperty("selectedEdition");
            string name = root.TryGetProperty("displayName", out JsonElement display) ? display.GetString() ?? string.Empty : string.Empty;
            string path = edition.GetProperty("url").GetString() ?? string.Empty;
            decimal price = edition.GetProperty("currentPriceInDecimal").GetDecimal();
            decimal original = PriceParser.TryParse(edition.GetProperty("rrp").GetString(), out decimal rrp) && rrp > price ? rrp : price;
            string? image = edition.TryGetProperty("imageUrl", out JsonElement img) ? img.GetString() : null;

            if (name.Length == 0 || !Uri.TryCreate(baseUri, path, out Uri? url))
            {
                return false;
            }

            offer = new StoreOffer(store.Name, name, price, original, url.ToString(), store.Currency, image);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"init\('([A-Za-z0-9+/=]+)'")]
    private static partial Regex Payload();
}
