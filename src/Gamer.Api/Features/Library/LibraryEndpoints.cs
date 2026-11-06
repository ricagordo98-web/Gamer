using Gamer.Api.Features.Steam;
using Gamer.Contracts.Library;

namespace Gamer.Api.Features.Library;

public static class LibraryEndpoints
{
    public static IEndpointRouteBuilder MapLibraryEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/library").RequireAuthorization();

        group.MapGet("/sources", async (LibrarySourceService sources, CancellationToken ct) =>
            Results.Ok(await sources.ListAsync(ct)));

        group.MapPost("/sources/{id}/sync", async (string id, LibrarySourceService sources, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await sources.SyncAsync(id, ct));
            }
            catch (UnknownSourceException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
            }
            catch (Exception exception) when (exception is LibraryScanException or SteamNotConfiguredException)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
            }
            catch (SteamException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        group.MapPut("/sources/emulator", async (EmulatorPathRequest request, LibrarySourceService sources, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                return Results.Problem("Indica a pasta.", statusCode: StatusCodes.Status400BadRequest);
            }

            await sources.SaveEmulatorPathAsync(request.Path, ct);
            return Results.Ok(await sources.ListAsync(ct));
        });

        return app;
    }
}
