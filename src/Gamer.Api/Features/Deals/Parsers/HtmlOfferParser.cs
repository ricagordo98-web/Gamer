using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Parsers;

public record HtmlSelectors(string Item, string Name, string Price, string OriginalPrice, string Link);

public class HtmlOfferParser(HtmlSelectors selectors) : IOfferParser
{
    public IReadOnlyList<StoreOffer> Parse(string content, Store store)
    {
        IDocument document = new HtmlParser().ParseDocument(content);
        IHtmlCollection<IElement> items = document.QuerySelectorAll(selectors.Item);
        if (items.Length == 0)
        {
            throw new ParseException("No result items found in the page.");
        }

        Uri baseUri = new(store.BaseUrl);
        List<StoreOffer> offers = [];
        foreach (IElement item in items)
        {
            string name = item.QuerySelector(selectors.Name)?.TextContent.Trim() ?? string.Empty;
            if (name.Length == 0 || !PriceParser.TryParse(item.QuerySelector(selectors.Price)?.TextContent, out decimal price))
            {
                continue;
            }

            decimal original = PriceParser.TryParse(item.QuerySelector(selectors.OriginalPrice)?.TextContent, out decimal parsed) && parsed > price
                ? parsed
                : price;
            string? href = (item.QuerySelector(selectors.Link) ?? item).GetAttribute("href");
            string url = href is not null && Uri.TryCreate(baseUri, href, out Uri? absolute) ? absolute.ToString() : store.BaseUrl;
            offers.Add(new StoreOffer(store.Name, name, price, original, url, store.Currency));
        }

        return offers;
    }
}
