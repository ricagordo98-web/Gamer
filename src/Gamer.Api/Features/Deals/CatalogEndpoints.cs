using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Deals;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/catalog", async ([AsParameters] CatalogQuery query, bool? refresh, ICatalogService catalog, CancellationToken ct) =>
            await catalog.QueryAsync(query, refresh ?? false, ct) is { } page
                ? Results.Ok(page)
                : Results.Problem(title: "Loja desconhecida.", statusCode: StatusCodes.Status404NotFound))
            .RequireAuthorization();

        return app;
    }
}
