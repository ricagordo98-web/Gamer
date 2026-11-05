using Gamer.Contracts.Deals;

namespace Gamer.Ui.Games;

public static class BundlePricing
{
    public static int MaxPicks(IReadOnlyList<BundleTierDto> tiers) => tiers.Count == 0 ? 0 : tiers.Max(t => t.Count);

    public static BundleTierDto PackFor(IReadOnlyList<BundleTierDto> tiers, int picked) =>
        tiers.OrderBy(t => t.Count).FirstOrDefault(t => t.Count >= picked) ?? tiers.MaxBy(t => t.Count)!;
}
