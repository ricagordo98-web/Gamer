using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Parsers;

public record HtmlSelectors(string Item, string Name, string Price, string OriginalPrice, string Link, string Image, string Discount = "");

public partial class HtmlOfferParser(HtmlSelectors selectors, Func<string, string>? normalizeName = null) : IOfferParser
{
    public IReadOnlyList<StoreOffer> Parse(string content, Store store)
    {
        IDocument document = new HtmlParser().ParseDocument(content);
        IHtmlCollection<IElement> items = document.QuerySelectorAll(selectors.Item);
        if (items.Length == 0)
        {
            throw new NoResultsException("No result items found in the page.");
        }

        Uri baseUri = new(store.BaseUrl);
        List<StoreOffer> offers = [];
        foreach (IElement item in items)
        {
            string name = ReadName(item);
            if (name.Length == 0 || !PriceParser.TryParse(item.QuerySelector(selectors.Price)?.TextContent, out decimal price))
            {
                continue;
            }

            decimal original = ReadOriginalPrice(item, price);
            offers.Add(new StoreOffer(store.Name, name, price, original, ReadUrl(item, baseUri, store), store.Currency, ReadImage(item, baseUri)));
        }

        return offers;
    }

    private string ReadName(IElement item)
    {
        IElement? element = item.QuerySelector(selectors.Name);
        string name = element?.TextContent.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            name = (element?.GetAttribute("alt") ?? element?.GetAttribute("title") ?? string.Empty).Trim();
        }

        name = Spaces().Replace(name, " ");
        return normalizeName is null ? name : normalizeName(name).Trim();
    }

    private decimal ReadOriginalPrice(IElement item, decimal price)
    {
        if (PriceParser.TryParse(item.QuerySelector(selectors.OriginalPrice)?.TextContent, out decimal parsed) && parsed > price)
        {
            return parsed;
        }

        string? discountText = selectors.Discount.Length == 0 ? null : item.QuerySelector(selectors.Discount)?.TextContent;
        Match match = Percent().Match(discountText ?? string.Empty);
        if (match.Success && int.TryParse(match.Value, out int percent) && percent is > 0 and < 100)
        {
            return Math.Round(price / (1m - percent / 100m), 2, MidpointRounding.AwayFromZero);
        }

        return price;
    }

    private string ReadUrl(IElement item, Uri baseUri, Store store)
    {
        string? href = (item.QuerySelector(selectors.Link) ?? item).GetAttribute("href");
        return href is not null && Uri.TryCreate(baseUri, href, out Uri? absolute) ? absolute.ToString() : store.BaseUrl;
    }

    private string? ReadImage(IElement item, Uri baseUri)
    {
        IElement? image = item.QuerySelector(selectors.Image);
        string? source = image?.GetAttribute("src");
        if (string.IsNullOrWhiteSpace(source) || source.StartsWith("data:", StringComparison.Ordinal) || source.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
        {
            source = image?.GetAttribute("data-src");
        }

        return !string.IsNullOrWhiteSpace(source) && Uri.TryCreate(baseUri, source, out Uri? absolute) ? absolute.ToString() : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\d+")]
    private static partial Regex Percent();
}
