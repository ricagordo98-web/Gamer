using FluentValidation;
using Gamer.Api.Infrastructure;
using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals;

public class TrackRequestValidator : AbstractValidator<TrackRequest>
{
    public TrackRequestValidator() => RuleFor(r => r.Title).NotEmpty().MaximumLength(200);
}

public static class DealEndpoints
{
    public static IEndpointRouteBuilder MapDealEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/deals").RequireAuthorization();

        group.MapGet("/", async (IDealService deals, CancellationToken ct) =>
            Results.Ok(await deals.GetDealsAsync(ct)));

        group.MapGet("/search", async (string? q, IDealService deals, CancellationToken ct) =>
            string.IsNullOrWhiteSpace(q) || q.Length > 200
                ? Results.ValidationProblem(new Dictionary<string, string[]> { ["q"] = ["Indica o nome do jogo."] })
                : Results.Ok(await deals.SearchAsync(q, ct)));

        group.MapPost("/tracked", async (TrackRequest request, IDealService deals, CancellationToken ct) =>
            Results.Ok(await deals.TrackAsync(request.Title, ct)))
            .AddEndpointFilter<ValidationFilter<TrackRequest>>();

        group.MapDelete("/tracked/{id:int}", async (int id, IDealService deals, CancellationToken ct) =>
            await deals.UntrackAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        group.MapPost("/refresh", (RefreshCoordinator coordinator) =>
        {
            coordinator.TryStart();
            return Results.Accepted(value: coordinator.Status);
        });

        group.MapGet("/refresh", (RefreshCoordinator coordinator) => Results.Ok(coordinator.Status));

        return app;
    }
}
