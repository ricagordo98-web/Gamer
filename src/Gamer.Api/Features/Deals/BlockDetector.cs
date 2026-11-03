namespace Gamer.Api.Features.Deals;

public static class BlockDetector
{
    private static readonly string[] Markers =
    [
        "Attention Required! | Cloudflare",
        "Just a moment...",
        "cf-browser-verification",
    ];

    public static bool IsBlocked(int statusCode, string body) =>
        statusCode is 403 or 429 or 503
        || Markers.Any(marker => body.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
