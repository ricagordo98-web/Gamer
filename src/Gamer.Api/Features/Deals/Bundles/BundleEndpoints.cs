namespace Gamer.Api.Features.Deals.Bundles;

public static class BundleEndpoints
{
    public static IEndpointRouteBuilder MapBundleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/bundles", async (string? store, bool? refresh, IBundleService bundles, CancellationToken ct) =>
            await bundles.QueryAsync(store, refresh ?? false, ct) is { } page
                ? Results.Ok(page)
                : Results.Problem(title: "Esta loja não tem bundles.", statusCode: StatusCodes.Status404NotFound))
            .RequireAuthorization();

        return app;
    }
}
