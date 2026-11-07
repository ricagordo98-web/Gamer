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

        group.MapPut("/sources/{id}/path", async (string id, EmulatorPathRequest request, LibrarySourceService sources, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Path))
            {
                return Results.Problem("Indica a pasta.", statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                await sources.SaveSourcePathAsync(id, request.Path, ct);
            }
            catch (UnknownSourceException exception)
            {
                return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(await sources.ListAsync(ct));
        });

        group.MapGet("/duplicates", async (DuplicateService duplicates, CancellationToken ct) =>
            Results.Ok(await duplicates.ListAsync(ct)));

        group.MapPost("/duplicates/merge", async (MergeDuplicatesRequest request, DuplicateService duplicates, CancellationToken ct) =>
            request.RemoveIds.Count == 0
                ? Results.Problem("Escolhe os jogos a juntar.", statusCode: StatusCodes.Status400BadRequest)
                : Results.Ok(new { removed = await duplicates.MergeAsync(request.KeepId, request.RemoveIds, ct) }));

        group.MapPost("/duplicates/ignore", async (IgnoreDuplicatesRequest request, DuplicateService duplicates, CancellationToken ct) =>
        {
            await duplicates.IgnoreAsync(request.Ids, ct);
            return Results.NoContent();
        });

        return app;
    }
}
