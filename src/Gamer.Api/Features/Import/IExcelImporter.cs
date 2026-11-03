using Gamer.Contracts.Games;

namespace Gamer.Api.Features.Import;

public interface IExcelImporter
{
    Task<ImportResultDto> ImportAsync(Stream workbook, CancellationToken cancellationToken);
}
