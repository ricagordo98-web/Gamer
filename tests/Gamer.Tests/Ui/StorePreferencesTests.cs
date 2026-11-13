using FluentAssertions;
using Gamer.Ui.Services;
using NSubstitute;

namespace Gamer.Tests.Ui;

public class StorePreferencesTests
{
    private readonly IKeyValueStore _store = Substitute.For<IKeyValueStore>();

    [Fact]
    public async Task EnsureLoaded_NothingSaved_ShowsOwnedGames()
    {
        _store.GetAsync(StorageKeys.HideOwned).Returns(new ValueTask<string?>((string?)null));
        StorePreferences preferences = new(_store);

        await preferences.EnsureLoadedAsync();

        preferences.HideOwned.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureLoaded_SavedAsTrue_HidesOwnedGames()
    {
        _store.GetAsync(StorageKeys.HideOwned).Returns(new ValueTask<string?>(bool.TrueString));
        StorePreferences preferences = new(_store);

        await preferences.EnsureLoadedAsync();

        preferences.HideOwned.Should().BeTrue();
    }

    [Fact]
    public async Task SetHideOwned_PersistsTheChoice()
    {
        StorePreferences preferences = new(_store);

        await preferences.SetHideOwnedAsync(true);

        preferences.HideOwned.Should().BeTrue();
        await _store.Received(1).SetAsync(StorageKeys.HideOwned, bool.TrueString);
    }

    [Fact]
    public async Task EnsureLoaded_AfterSet_DoesNotOverwriteTheChoice()
    {
        _store.GetAsync(StorageKeys.HideOwned).Returns(new ValueTask<string?>((string?)null));
        StorePreferences preferences = new(_store);
        await preferences.SetHideOwnedAsync(true);

        await preferences.EnsureLoadedAsync();

        preferences.HideOwned.Should().BeTrue();
    }
}
