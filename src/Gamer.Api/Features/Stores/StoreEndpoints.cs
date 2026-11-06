using FluentValidation;
using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Deals;
using Gamer.Api.Infrastructure;
using Gamer.Contracts.Deals;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Stores;

public class StoreUpdateRequestValidator : AbstractValidator<StoreUpdateRequest>
{
    public StoreUpdateRequestValidator() =>
        RuleFor(r => r.BaseUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
            .WithMessage("O endereço da loja não é válido.");
}

public static class StoreEndpoints
{
    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/stores").RequireAuthorization();

        group.MapPost("/refresh", (RefreshCoordinator coordinator) =>
        {
            coordinator.TryStart(RefreshScope.Stores);
            return Results.Accepted(value: coordinator.Status);
        });

        group.MapGet("/", async (GamerDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Stores.AsNoTracking().OrderBy(s => s.Id).Select(s => ToDto(s)).ToListAsync(ct)));

        group.MapPut("/{id:int}", async (int id, StoreUpdateRequest request, GamerDbContext db, CancellationToken ct) =>
        {
            Store? store = await db.Stores.FirstOrDefaultAsync(s => s.Id == id, ct);
            if (store is null)
            {
                return Results.NotFound();
            }

            string baseUrl = request.BaseUrl.Trim();
            store.BaseUrl = baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/";
            store.IsEnabled = request.IsEnabled;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(store));
        }).AddEndpointFilter<ValidationFilter<StoreUpdateRequest>>();

        return app;
    }

    private static StoreDto ToDto(Store store) =>
        new(store.Id, store.Name, store.BaseUrl, store.IsEnabled, store.Region, store.Currency);
}
