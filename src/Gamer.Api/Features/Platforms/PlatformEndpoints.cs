using Gamer.Api.Data;
using Gamer.Contracts.Games;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Platforms;

public static class PlatformEndpoints
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/platforms", async (GamerDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Platforms
                .AsNoTracking()
                .OrderBy(p => p.Family)
                .ThenBy(p => p.Name)
                .Select(p => new PlatformDto(p.Id, p.Name, p.Family, p.Format))
                .ToListAsync(ct)))
            .RequireAuthorization();

        return app;
    }
}
