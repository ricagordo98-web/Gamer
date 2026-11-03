using Gamer.Api.Infrastructure;
using Gamer.Contracts.Games;

namespace Gamer.Api.Features.Games;

public static class GameEndpoints
{
    public static IEndpointRouteBuilder MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/games").RequireAuthorization();

        group.MapGet("/", async (IGameService games, CancellationToken ct) =>
            Results.Ok(await games.GetAllAsync(ct)));

        group.MapPost("/", async (GameRequest request, IGameService games, CancellationToken ct) =>
        {
            GameDto created = await games.CreateAsync(request, ct);
            return Results.Created($"/api/games/{created.Id}", created);
        }).AddEndpointFilter<ValidationFilter<GameRequest>>();

        group.MapPut("/{id:int}", async (int id, GameRequest request, IGameService games, CancellationToken ct) =>
        {
            GameDto? updated = await games.UpdateAsync(id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).AddEndpointFilter<ValidationFilter<GameRequest>>();

        group.MapPatch("/{id:int}/status", async (int id, StatusRequest request, IGameService games, CancellationToken ct) =>
        {
            GameDto? updated = await games.ChangeStatusAsync(id, request.Status, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        group.MapDelete("/{id:int}", async (int id, IGameService games, CancellationToken ct) =>
            await games.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
