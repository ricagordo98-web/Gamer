using Gamer.Api.Domain;

namespace Gamer.Api.Features.Deals.Parsers;

public interface IOfferParser
{
    IReadOnlyList<StoreOffer> Parse(string content, Store store);
}
