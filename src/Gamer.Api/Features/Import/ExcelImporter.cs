using ClosedXML.Excel;
using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Games;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Import;

public class ExcelImporter(GamerDbContext db, TimeProvider clock) : IExcelImporter
{
    private const string TitleHeader = "Jogo";
    private const string PlatformHeader = "Consola";
    private const string PlayingHeader = "A Jogar";
    private const string CompletedHeader = "Terminado";
    private const string DeckHeader = "Deck";

    public async Task<ImportResultDto> ImportAsync(Stream workbook, CancellationToken cancellationToken)
    {
        List<ExcelGameRow> rows = ReadRows(workbook);

        Dictionary<string, Platform> platforms = await db.Platforms
            .ToDictionaryAsync(p => p.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        HashSet<string> knownGames = (await db.Games
                .Select(g => new { g.Title, PlatformName = g.Platform.Name })
                .ToListAsync(cancellationToken))
            .Select(g => BuildKey(g.Title, g.PlatformName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> duplicates = [];
        int imported = 0;
        DateTimeOffset now = clock.GetUtcNow();

        foreach (ExcelGameRow row in rows)
        {
            ParsedPlatform parsed = PlatformParser.Parse(row.Platform);
            if (!knownGames.Add(BuildKey(row.Title, parsed.Name)))
            {
                duplicates.Add($"{row.Title} ({parsed.Name})");
                continue;
            }

            Platform platform = GetOrAddPlatform(platforms, parsed);
            db.Games.Add(new Game
            {
                Title = row.Title,
                Platform = platform,
                Status = StatusMapper.FromFlags(row.IsPlaying, row.IsCompleted),
                OnDeck = row.OnDeck,
                CreatedAt = now,
                UpdatedAt = now,
            });
            imported++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ImportResultDto(imported, duplicates.Count, duplicates);
    }

    private Platform GetOrAddPlatform(Dictionary<string, Platform> platforms, ParsedPlatform parsed)
    {
        if (platforms.TryGetValue(parsed.Name, out Platform? existing))
        {
            return existing;
        }

        Platform created = new() { Name = parsed.Name, Family = parsed.Family, Format = parsed.Format };
        db.Platforms.Add(created);
        platforms[parsed.Name] = created;
        return created;
    }

    private static string BuildKey(string title, string platformName) => $"{title}|{platformName}";

    private static List<ExcelGameRow> ReadRows(Stream workbook)
    {
        using XLWorkbook book = new(workbook);
        // A workbook can hold several lists (e.g. another person's sheet without a platform column), so use the
        // first sheet that has both the game and platform headers.
        (IXLWorksheet sheet, IXLRow headerRow) = FindListSheet(book);

        int titleColumn = FindColumn(headerRow, TitleHeader)!.Value;
        int platformColumn = FindColumn(headerRow, PlatformHeader) ?? throw MissingColumn(PlatformHeader);
        int playingColumn = FindColumn(headerRow, PlayingHeader) ?? throw MissingColumn(PlayingHeader);
        int completedColumn = FindColumn(headerRow, CompletedHeader) ?? throw MissingColumn(CompletedHeader);
        int deckColumn = FindColumn(headerRow, DeckHeader) ?? throw MissingColumn(DeckHeader);

        List<ExcelGameRow> rows = [];
        foreach (IXLRow row in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            string title = row.Cell(titleColumn).GetString().Trim();
            string platform = row.Cell(platformColumn).GetString().Trim();
            if (title.Length == 0 || platform.Length == 0)
            {
                continue;
            }

            rows.Add(new ExcelGameRow(
                title,
                platform,
                IsFlagSet(row.Cell(playingColumn)),
                IsFlagSet(row.Cell(completedColumn)),
                IsFlagSet(row.Cell(deckColumn))));
        }

        return rows;
    }

    private static (IXLWorksheet Sheet, IXLRow Header) FindListSheet(XLWorkbook book)
    {
        foreach (IXLWorksheet sheet in book.Worksheets)
        {
            IXLRow? header = sheet.RowsUsed().FirstOrDefault(r => FindColumn(r, TitleHeader) is not null && FindColumn(r, PlatformHeader) is not null);
            if (header is not null)
            {
                return (sheet, header);
            }
        }

        throw new InvalidDataException($"No sheet has the columns \"{TitleHeader}\" and \"{PlatformHeader}\".");
    }

    private static int? FindColumn(IXLRow row, string header) =>
        row.CellsUsed().FirstOrDefault(c => string.Equals(c.GetString().Trim(), header, StringComparison.OrdinalIgnoreCase))
            ?.Address.ColumnNumber;

    private static InvalidDataException MissingColumn(string header) => new($"Column \"{header}\" not found.");

    private static bool IsFlagSet(IXLCell cell) => cell.GetString().Trim() is "1" or "TRUE" or "true";

    private record ExcelGameRow(string Title, string Platform, bool IsPlaying, bool IsCompleted, bool OnDeck);
}
