using Gamer.Api.Domain;
using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals.Bundles;

public record StoreBundleItem(string Title, string? ImageUrl, decimal? Msrp, int Tier);

public record StoreBundle(
    string Title,
    string Url,
    string? ImageUrl,
    string? Blurb,
    DateTimeOffset? EndsAt,
    string Currency,
    string Kind,
    IReadOnlyList<BundleTierDto> Tiers,
    IReadOnlyList<StoreBundleItem> Items);

public interface IBundleSource
{
    string StoreName { get; }

    Task<IReadOnlyList<StoreBundle>> ListAsync(Store store, CancellationToken cancellationToken);
}
