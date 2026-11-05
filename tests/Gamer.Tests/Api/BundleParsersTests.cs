using FluentAssertions;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Deals.Bundles;
using Gamer.Contracts.Deals;

namespace Gamer.Tests.Api;

public sealed class BundleParsersTests
{
    private const string HumbleListingHtml = """
        <html><script id="landingPage-json-data" type="application/json">
        {"data":{"games":{"mosaic":[{"products":[
          {"category":"bundle","tile_name":"Double Fine Bundle","product_url":"/games/double-fine","high_res_tile_image":"https://img/big.jpg","short_marketing_blurb":"Psychic fun","end_date|datetime":"2026-11-12T05:00:00"},
          {"category":"storefront","tile_name":"Not a bundle","product_url":"/store/x"},
          {"category":"bundle","tile_name":"Double Fine Bundle","product_url":"/games/double-fine"}
        ]}]}}}
        </script></html>
        """;

    private const string HumbleBundleHtml = """
        <html><script id="webpack-bundle-page-data" type="application/json">
        {"bundleData":{
          "basic_data":{"currency":"EUR"},
          "tier_order":["bt8","initial"],
          "tier_pricing_data":{
            "initial":{"price|money":{"currency":"EUR","amount":5.49}},
            "bt8":{"price|money":{"currency":"EUR","amount":8.79}}},
          "tier_display_data":{
            "initial":{"tier_item_machine_names":["stacking","devs"]},
            "bt8":{"tier_item_machine_names":["psychonauts","stacking","devs"]}},
          "tier_item_data":{
            "stacking":{"human_name":"Stacking","item_content_type":"game","msrp_price|money":{"amount":9.99},"resolved_paths":{"featured_image":"https://img/stacking.png"}},
            "psychonauts":{"human_name":"Psychonauts","item_content_type":"game","msrp_price|money":{"amount":14.99},"resolved_paths":{"featured_image":"https://img/psy.png"}},
            "devs":{"human_name":"Day of the Devs","item_content_type":null}}
        }}
        </script></html>
        """;

    private const string FanaticalJson = """
        {"pickandmix":[
          {"name":"Build your own Killer Bundle","slug":"killer","type":"bundle","cover_image":"cover.jpeg",
           "tiers":[{"quantity":5,"price":{"EUR":1499}},{"quantity":3,"price":{"EUR":999}}],
           "products":[{"name":"Hades","cover":"hades.jpeg"},{"name":"Celeste","cover":"celeste.jpeg"}]},
          {"name":"Python Books","slug":"python","type":"book-bundle","tiers":[{"quantity":3,"price":{"EUR":500}}],"products":[{"name":"Book"}]}
        ]}
        """;

    [Fact]
    public void ParseListing_ReturnsDistinctGameBundles()
    {
        IReadOnlyList<HumbleListing> listings = HumbleBundleParser.ParseListing(HumbleListingHtml, "https://www.humblebundle.com/");

        HumbleListing listing = listings.Should().ContainSingle().Subject;
        listing.Url.Should().Be("https://www.humblebundle.com/games/double-fine");
        listing.ImageUrl.Should().Be("https://img/big.jpg");
        listing.EndsAt.Should().Be(new DateTimeOffset(2026, 11, 12, 5, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ParseBundle_OrdersTiersByPrice_AndAssignsEachGameItsCheapestTier()
    {
        HumbleListing listing = new("Double Fine Bundle", "https://hb/games/df", null, null, null);

        StoreBundle bundle = HumbleBundleParser.ParseBundle(HumbleBundleHtml, listing)!;

        bundle.Kind.Should().Be(BundleKinds.Tiered);
        bundle.Tiers.Should().Equal(new BundleTierDto(5.49m, 1), new BundleTierDto(8.79m, 2));
        bundle.Items.Select(i => (i.Title, i.Tier)).Should().Equal(("Stacking", 0), ("Psychonauts", 1));
        bundle.Items[1].ImageUrl.Should().Be("https://img/psy.png");
    }

    [Fact]
    public void ParseBundle_MissingData_Throws()
    {
        Action parse = () => HumbleBundleParser.ParseBundle("<html></html>", new HumbleListing("x", "u", null, null, null));

        parse.Should().Throw<ScrapeException>();
    }

    [Fact]
    public void FanaticalParse_KeepsOnlyGameBundles_WithTiersInEuros()
    {
        IReadOnlyList<StoreBundle> bundles = FanaticalBundleParser.Parse(FanaticalJson, "https://www.fanatical.com/", "EUR");

        StoreBundle bundle = bundles.Should().ContainSingle().Subject;
        bundle.Kind.Should().Be(BundleKinds.Pick);
        bundle.Url.Should().Be("https://www.fanatical.com/en/pick-and-mix/killer");
        bundle.Tiers.Should().Equal(new BundleTierDto(9.99m, 3), new BundleTierDto(14.99m, 5));
        bundle.Items.Select(i => i.Title).Should().Equal("Hades", "Celeste");
        bundle.Items[0].ImageUrl.Should().StartWith("https://fanatical.imgix.net/product/original/hades.jpeg");
    }
}
