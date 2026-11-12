using Gamer.Contracts.Stats;

namespace Gamer.Api.Features.Stats;

public static class StatsEndpoints
{
    private const int MaxPlayerIds = 30;

    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/stats").RequireAuthorization();

        group.MapGet("/", async (StatsService stats, CancellationToken ct) =>
            Results.Ok(await stats.GetAsync(ct)));

        group.MapGet("/games", async (StatsService stats, CancellationToken ct) =>
            Results.Ok(await stats.GetGamesAsync(ct)));

        group.MapGet("/apps/{appId:long}", async (long appId, StatsService stats, CancellationToken ct) =>
            await stats.GetAppAsync(appId, ct) is { } detail ? Results.Ok(detail) : Results.NotFound());

        group.MapGet("/players", async (string ids, PlayerCountCache cache, CancellationToken ct) =>
        {
            List<long> appIds = ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(id => long.TryParse(id, out long parsed) ? parsed : 0)
                .Where(id => id > 0)
                .Take(MaxPlayerIds)
                .ToList();
            return Results.Ok(await cache.GetManyAsync(appIds, ct));
        });

        group.MapPost("/enrich", (SteamEnrichmentRunner runner) =>
        {
            runner.TryStart();
            return Results.Ok(runner.Status);
        });

        group.MapGet("/enrich", (SteamEnrichmentRunner runner) => Results.Ok(runner.Status));

        return app;
    }
}
