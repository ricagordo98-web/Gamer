using Gamer.Api.Data;
using Gamer.Api.Infrastructure;
using Gamer.Contracts.Steam;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Steam;

public static class SteamEndpoints
{
    public static IEndpointRouteBuilder MapSteamEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/steam").RequireAuthorization();

        group.MapGet("/settings", async (ISteamSettingsService settings, CancellationToken ct) =>
            Results.Ok(await settings.GetAsync(ct)));

        group.MapPut("/settings", async (SteamSettingsRequest request, ISteamSettingsService settings, CancellationToken ct) =>
        {
            await settings.SaveAsync(request, ct);
            return Results.Ok(await settings.GetAsync(ct));
        }).AddEndpointFilter<ValidationFilter<SteamSettingsRequest>>();

        group.MapPost("/sync", async (SteamSyncService sync, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await sync.SyncAsync(ct));
            }
            catch (SteamNotConfiguredException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (SteamException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapGet("/library", async (GamerDbContext db, CancellationToken ct) =>
            Results.Ok(await db.SteamLibrary.AsNoTracking()
                .OrderBy(i => i.Title)
                .Select(i => new SteamLibraryItemDto(i.AppId, i.Title, i.PlaytimeMinutes))
                .ToListAsync(ct)));

        group.MapGet("/wishlist", async (GamerDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Wishlist.AsNoTracking()
                .OrderBy(i => i.Title)
                .Select(i => new WishlistItemDto(i.AppId, i.Title, i.AddedAt))
                .ToListAsync(ct)));

        return app;
    }
}
