namespace Gamer.Api.Features.Deals.Parsers;

public static class SteamImages
{
    public static string Header(long appId) => $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg";
}
