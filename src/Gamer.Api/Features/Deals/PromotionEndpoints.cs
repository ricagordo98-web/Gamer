using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals;

public static class PromotionEndpoints
{
    public static IEndpointRouteBuilder MapPromotionEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/promotions").RequireAuthorization();

        group.MapGet("/", async ([AsParameters] PromotionQuery query, IPromotionService promotions, CancellationToken ct) =>
            Results.Ok(await promotions.QueryAsync(query, ct)));

        group.MapPost("/refresh", (RefreshCoordinator coordinator) =>
        {
            coordinator.TryStart(RefreshScope.Promotions);
            return Results.Accepted(value: coordinator.Status);
        });

        return app;
    }
}
