using FluentAssertions;
using Gamer.Api.Features.Games;
using Gamer.Contracts.Games;

namespace Gamer.Tests.Api;

public sealed class GameServiceTests : IDisposable
{
    private const int Ps2PlatformId = 2;
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private GameService CreateService() => new(_database.CreateContext(), TimeProvider.System);

    private static GameRequest NewRequest(string title = "Hades") =>
        new(title, Ps2PlatformId, GameStatus.Backlog, false, null, null);

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsGameWithPlatformData()
    {
        GameDto created = await CreateService().CreateAsync(NewRequest("  Hades  "), CancellationToken.None);

        created.Title.Should().Be("Hades");
        created.PlatformName.Should().Be("PS2");
        created.Status.Should().Be(GameStatus.Backlog);
    }

    [Fact]
    public async Task GetAllAsync_MultipleGames_ReturnsOrderedByTitle()
    {
        GameService service = CreateService();
        await service.CreateAsync(NewRequest("Zelda"), CancellationToken.None);
        await service.CreateAsync(NewRequest("Alone in the Dark"), CancellationToken.None);

        IReadOnlyList<GameDto> games = await service.GetAllAsync(CancellationToken.None);

        games.Select(g => g.Title).Should().Equal("Alone in the Dark", "Zelda");
    }

    [Fact]
    public async Task UpdateAsync_ExistingGame_ChangesFields()
    {
        GameService service = CreateService();
        GameDto created = await service.CreateAsync(NewRequest(), CancellationToken.None);

        GameDto? updated = await service.UpdateAsync(
            created.Id,
            new GameRequest("Hades II", Ps2PlatformId, GameStatus.Playing, true, 9, "Great"),
            CancellationToken.None);

        updated.Should().NotBeNull();
        updated!.Title.Should().Be("Hades II");
        updated.Status.Should().Be(GameStatus.Playing);
        updated.OnDeck.Should().BeTrue();
        updated.Rating.Should().Be(9);
    }

    [Fact]
    public async Task UpdateAsync_MissingGame_ReturnsNull()
    {
        GameDto? result = await CreateService().UpdateAsync(999, NewRequest(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ChangeStatusAsync_ExistingGame_UpdatesStatus()
    {
        GameService service = CreateService();
        GameDto created = await service.CreateAsync(NewRequest(), CancellationToken.None);

        GameDto? result = await service.ChangeStatusAsync(created.Id, GameStatus.Completed, CancellationToken.None);

        result!.Status.Should().Be(GameStatus.Completed);
    }

    [Fact]
    public async Task DeleteAsync_ExistingGame_RemovesIt()
    {
        GameService service = CreateService();
        GameDto created = await service.CreateAsync(NewRequest(), CancellationToken.None);

        bool deleted = await service.DeleteAsync(created.Id, CancellationToken.None);

        deleted.Should().BeTrue();
        (await service.GetAllAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_MissingGame_ReturnsFalse()
    {
        (await CreateService().DeleteAsync(999, CancellationToken.None)).Should().BeFalse();
    }
}
