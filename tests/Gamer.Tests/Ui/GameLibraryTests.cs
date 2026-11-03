using FluentAssertions;
using Gamer.Contracts.Games;
using Gamer.Ui.Games;
using Gamer.Ui.Services;
using NSubstitute;

namespace Gamer.Tests.Ui;

public class GameLibraryTests
{
    private static GameDto Game(int id, string title, GameStatus status = GameStatus.Backlog) =>
        new(id, title, 1, "PS2", "PlayStation", PlatformFormat.Physical, status, false, null, null);

    private readonly IGamerApi _api = Substitute.For<IGamerApi>();
    private readonly ToastService _toasts = new();

    private GameLibrary CreateLibrary()
    {
        _api.GetGamesAsync(Arg.Any<CancellationToken>()).Returns([Game(2, "Zelda"), Game(1, "Alone")]);
        _api.GetPlatformsAsync(Arg.Any<CancellationToken>()).Returns([new PlatformDto(1, "PS2", "PlayStation", PlatformFormat.Physical)]);
        return new GameLibrary(_api, _toasts);
    }

    [Fact]
    public async Task ReloadAsync_Success_LoadsGamesSortedByTitle()
    {
        GameLibrary library = CreateLibrary();

        await library.ReloadAsync(CancellationToken.None);

        library.IsLoaded.Should().BeTrue();
        library.Games.Select(g => g.Title).Should().Equal("Alone", "Zelda");
    }

    [Fact]
    public async Task ReloadAsync_ApiFails_ExposesLoadError()
    {
        GameLibrary library = CreateLibrary();
        _api.GetGamesAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<GameDto>>(_ => throw new ApiException("offline"));

        await library.ReloadAsync(CancellationToken.None);

        library.IsLoaded.Should().BeFalse();
        library.LoadError.Should().Be("offline");
    }

    [Fact]
    public async Task SetStatusAsync_Success_ReplacesGameInList()
    {
        GameLibrary library = CreateLibrary();
        await library.ReloadAsync(CancellationToken.None);
        GameDto alone = library.Games.First(g => g.Id == 1);
        _api.ChangeStatusAsync(1, GameStatus.Playing, Arg.Any<CancellationToken>()).Returns(alone with { Status = GameStatus.Playing });

        bool ok = await library.SetStatusAsync(alone, GameStatus.Playing, CancellationToken.None);

        ok.Should().BeTrue();
        library.Games.First(g => g.Id == 1).Status.Should().Be(GameStatus.Playing);
    }

    [Fact]
    public async Task SaveAsync_ApiRejects_ShowsErrorToastAndReturnsFalse()
    {
        GameLibrary library = CreateLibrary();
        _api.CreateGameAsync(Arg.Any<GameRequest>(), Arg.Any<CancellationToken>()).Returns<GameDto>(_ => throw new ApiException("Título inválido"));

        bool ok = await library.SaveAsync(null, new GameRequest("x", 1, GameStatus.Backlog, false, null, null), CancellationToken.None);

        ok.Should().BeFalse();
        _toasts.Toasts.Should().ContainSingle(t => t.Kind == ToastKind.Error);
    }

    [Fact]
    public async Task DeleteAsync_Success_RemovesGame()
    {
        GameLibrary library = CreateLibrary();
        await library.ReloadAsync(CancellationToken.None);

        await library.DeleteAsync(1, CancellationToken.None);

        library.Games.Should().NotContain(g => g.Id == 1);
    }
}
