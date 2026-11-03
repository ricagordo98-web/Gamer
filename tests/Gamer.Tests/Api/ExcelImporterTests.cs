using ClosedXML.Excel;
using FluentAssertions;
using Gamer.Api.Features.Import;
using Gamer.Contracts.Games;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Tests.Api;

public sealed class ExcelImporterTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private static MemoryStream BuildWorkbook(params object[][] rows)
    {
        using XLWorkbook book = new();
        IXLWorksheet sheet = book.AddWorksheet("Ricardo");
        string[] headers = ["Jogo", "Consola", "A Jogar", "Terminado", "Deck"];
        for (int column = 0; column < headers.Length; column++)
        {
            sheet.Cell(2, column + 2).Value = headers[column];
        }

        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < rows[row].Length; column++)
            {
                sheet.Cell(row + 3, column + 2).Value = XLCellValue.FromObject(rows[row][column]);
            }
        }

        MemoryStream stream = new();
        book.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private ExcelImporter CreateImporter() => new(_database.CreateContext(), TimeProvider.System);

    [Fact]
    public async Task ImportAsync_ValidRows_ImportsGamesWithMappedStatusAndPlatform()
    {
        using MemoryStream workbook = BuildWorkbook(
            ["Hades ", "PC - STEAM", 1, 0, 1],
            ["Ace Combat 2", "L - PS1", 0, 1, 0],
            ["Alone in the Dark", "L - PS2", 0, 0, 0]);

        ImportResultDto result = await CreateImporter().ImportAsync(workbook, CancellationToken.None);

        result.Imported.Should().Be(3);
        List<GameDto> games = await _database.CreateContext().Games
            .Select(g => new GameDto(g.Id, g.Title, g.PlatformId, g.Platform.Name, g.Platform.Family, g.Platform.Format, g.Status, g.OnDeck, g.Rating, g.Notes))
            .ToListAsync();
        games.Should().Contain(g => g.Title == "Hades" && g.Status == GameStatus.Playing && g.OnDeck && g.PlatformName == "Steam");
        games.Should().Contain(g => g.Title == "Ace Combat 2" && g.Status == GameStatus.Completed);
        games.Should().Contain(g => g.Title == "Alone in the Dark" && g.Status == GameStatus.Backlog && !g.OnDeck);
    }

    [Fact]
    public async Task ImportAsync_DuplicateRows_SkipsAndReportsThem()
    {
        using MemoryStream workbook = BuildWorkbook(
            ["Hades", "PC - STEAM", 0, 0, 0],
            ["Hades ", "PC - STEAM", 0, 0, 0]);

        ImportResultDto result = await CreateImporter().ImportAsync(workbook, CancellationToken.None);

        result.Imported.Should().Be(1);
        result.Skipped.Should().Be(1);
        result.Duplicates.Should().ContainSingle().Which.Should().Contain("Hades");
    }

    [Fact]
    public async Task ImportAsync_RunTwice_DoesNotDuplicateGames()
    {
        using MemoryStream first = BuildWorkbook(["Hades", "PC - STEAM", 0, 0, 0]);
        using MemoryStream second = BuildWorkbook(["Hades", "PC - STEAM", 0, 0, 0]);
        await CreateImporter().ImportAsync(first, CancellationToken.None);

        ImportResultDto result = await CreateImporter().ImportAsync(second, CancellationToken.None);

        result.Imported.Should().Be(0);
        result.Skipped.Should().Be(1);
    }

    [Fact]
    public async Task ImportAsync_UnknownPlatform_CreatesIt()
    {
        using MemoryStream workbook = BuildWorkbook(["Sonic", "L - Dreamcast", 0, 0, 0]);

        await CreateImporter().ImportAsync(workbook, CancellationToken.None);

        (await _database.CreateContext().Platforms.AnyAsync(p => p.Name == "Dreamcast")).Should().BeTrue();
    }

    [Fact]
    public async Task ImportAsync_MissingHeaders_ThrowsInvalidData()
    {
        using XLWorkbook book = new();
        book.AddWorksheet("Empty").Cell(1, 1).Value = "Nothing";
        using MemoryStream stream = new();
        book.SaveAs(stream);
        stream.Position = 0;

        Func<Task> act = () => CreateImporter().ImportAsync(stream, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>();
    }
}
