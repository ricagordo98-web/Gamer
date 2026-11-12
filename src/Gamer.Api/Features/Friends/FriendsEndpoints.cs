using Gamer.Api.Features.Steam;

namespace Gamer.Api.Features.Friends;

public static class FriendsEndpoints
{
    public static IEndpointRouteBuilder MapFriendsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/friends", async (bool? refresh, FriendsService friends, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await friends.GetAsync(refresh ?? false, ct));
            }
            catch (SteamNotConfiguredException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (SteamException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        }).RequireAuthorization();

        return app;
    }
}
