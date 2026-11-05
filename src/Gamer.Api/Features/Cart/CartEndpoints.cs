using Gamer.Api.Infrastructure;
using Gamer.Contracts.Deals;

namespace Gamer.Api.Features.Cart;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/cart").RequireAuthorization();

        group.MapGet("/", async (ICartService cart, CancellationToken ct) =>
            Results.Ok(await cart.GetAllAsync(ct)));

        group.MapPost("/", async (CartItemRequest request, ICartService cart, CancellationToken ct) =>
            Results.Ok(await cart.SaveAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<CartItemRequest>>();

        group.MapDelete("/{id:int}", async (int id, ICartService cart, CancellationToken ct) =>
            await cart.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
