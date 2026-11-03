using Gamer.Contracts.Games;

namespace Gamer.Api.Features.Import;

public static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/import", async (IFormFile file, IExcelImporter importer, CancellationToken ct) =>
        {
            await using Stream stream = file.OpenReadStream();
            try
            {
                ImportResultDto result = await importer.ImportAsync(stream, ct);
                return Results.Ok(result);
            }
            catch (InvalidDataException exception)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid spreadsheet.", detail: exception.Message);
            }
        }).RequireAuthorization().DisableAntiforgery();

        return app;
    }
}
