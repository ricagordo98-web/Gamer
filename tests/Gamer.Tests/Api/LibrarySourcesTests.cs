using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Library;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Games;
using Gamer.Contracts.Library;

namespace Gamer.Tests.Api;

public sealed class LibrarySourcesTests : IClassFixture<GamerApiFactory>
{
    private readonly GamerApiFactory _factory;

    public LibrarySourcesTests(GamerApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("Mario Kart - Double Dash!! (Europe) (En,Fr,De,Es,It).iso", "Mario Kart - Double Dash!!")]
    [InlineData("Luigi's Mansion (USA).nkit.iso", "Luigi's Mansion")]
    [InlineData("Banjo-Kazooie (U) [!].z64", "Banjo-Kazooie")]
    [InlineData("Armored Core - Master of Arena (USA) (Disc 1).zip", "Armored Core - Master of Arena")]
    public void Clean_RomFileName_RemovesTagsAndExtensions(string fileName, string expected) =>
        EmulatorTitles.Clean(fileName).Should().Be(expected);

    [Fact]
    public void Clean_FolderWithSerial_RemovesTagsAndSerial() =>
        EmulatorTitles.Clean("Dark Souls 2 [+ Fix All DLC] BLUS41045", hasExtension: false).Should().Be("Dark Souls 2");

    [Fact]
    public void ScanFolder_ReadsConsolesSkipsBiosAndJunk_WithoutTouchingFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "gamer-emu-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "BIOS"));
            Directory.CreateDirectory(Path.Combine(root, "PS1"));
            Directory.CreateDirectory(Path.Combine(root, "PS3", "Demon's Souls (USA)"));
            File.WriteAllText(Path.Combine(root, "BIOS", "scph1001.bin"), "x");
            File.WriteAllText(Path.Combine(root, "PS1", "Diablo (Europe).cue"), "x");
            File.WriteAllText(Path.Combine(root, "PS1", "Diablo (Europe).bin"), "x");
            File.WriteAllText(Path.Combine(root, "PS1", "Armored Core (USA) (Disc 1).zip"), "x");
            File.WriteAllText(Path.Combine(root, "PS1", "Armored Core (USA) (Disc 2).zip"), "x");
            File.WriteAllText(Path.Combine(root, "PS1", "info.txt"), "x");
            string[] before = Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories);

            IReadOnlyList<ScannedGame> games = EmulatorScanner.ScanFolder(root);

            games.Select(g => (g.Title, g.PlatformName)).Should().BeEquivalentTo(
            [
                ("Diablo", "PS1"),
                ("Armored Core", "PS1"),
                ("Demon's Souls", "PS3"),
            ]);
            games.Should().OnlyContain(g => g.PlatformFamily == "PlayStation");
            Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories).Should().BeEquivalentTo(before);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ParseTitle_EpicManifest_SkipsAddOns()
    {
        EpicManifestParser.ParseTitle("""{"DisplayName":"Hades","AppName":"a1","MainGameAppName":"a1"}""").Should().Be("Hades");
        EpicManifestParser.ParseTitle("""{"DisplayName":"Hades DLC","AppName":"a2","MainGameAppName":"a1"}""").Should().BeNull();
        EpicManifestParser.ParseTitle("not json").Should().BeNull();
    }

    [Fact]
    public void GamesOf_FiltersByPublisherAndExcludedNames()
    {
        InstalledProgram[] programs =
        [
            new("Assassin's Creed Origins", "Ubisoft"),
            new("Ubisoft Connect", "Ubisoft"),
            new("7-Zip", "Igor Pavlov"),
        ];

        InstalledPrograms.GamesOf(programs, "Ubisoft", "Ubisoft Connect").Should().Equal("Assassin's Creed Origins");
    }

    [Fact]
    public async Task EmulatorSource_SaveFolderAndSync_AddsEmulatorGamesOnce()
    {
        string root = Path.Combine(Path.GetTempPath(), "gamer-emu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "SNES"));
        File.WriteAllText(Path.Combine(root, "SNES", "ActRaiser (Europe).sfc"), "x");
        try
        {
            HttpClient client = await CreateAuthorizedClientAsync();
            await client.PutAsJsonAsync("/api/library/sources/emulator", new EmulatorPathRequest(root));

            LibrarySyncResultDto? first = await (await client.PostAsync("/api/library/sources/emulator/sync", null)).Content.ReadFromJsonAsync<LibrarySyncResultDto>();
            LibrarySyncResultDto? second = await (await client.PostAsync("/api/library/sources/emulator/sync", null)).Content.ReadFromJsonAsync<LibrarySyncResultDto>();
            List<GameDto>? games = await client.GetFromJsonAsync<List<GameDto>>("/api/games");

            first.Should().Be(new LibrarySyncResultDto(1, 1));
            second.Should().Be(new LibrarySyncResultDto(1, 0));
            games.Should().Contain(g => g.Title == "ActRaiser" && g.PlatformName == "SNES" && g.PlatformFamily == "Nintendo");
            File.Exists(Path.Combine(root, "SNES", "ActRaiser (Europe).sfc")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task LaunchBoxSource_ReadsStoresAndConsoles_WithoutTouchingFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), "gamer-lb-" + Guid.NewGuid().ToString("N"));
        string platforms = Path.Combine(root, "Data", "Platforms");
        Directory.CreateDirectory(platforms);
        File.WriteAllText(Path.Combine(platforms, "PC - Steam.xml"), "<LaunchBox><Game><Title>Hades</Title><Platform>PC - Steam</Platform><StorefrontPluginId>unbroken.steam</StorefrontPluginId></Game><Game><Title>Hidden</Title><Platform>PC - Steam</Platform><StorefrontPluginId>unbroken.steam</StorefrontPluginId><Hide>true</Hide></Game><Game><Title>Assassin&apos;s Creed</Title><Platform>PC - Ubisoft</Platform><StorefrontPluginId>unbroken.storefront.ubisoft</StorefrontPluginId></Game></LaunchBox>");
        File.WriteAllText(Path.Combine(platforms, "Sony Playstation 2.xml"), "<LaunchBox><Game><Title>Onimusha 3</Title><Platform>Sony Playstation 2</Platform><StorefrontPluginId></StorefrontPluginId></Game></LaunchBox>");
        string[] before = Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories);
        try
        {
            HttpClient client = await CreateAuthorizedClientAsync();
            await client.PutAsJsonAsync("/api/library/sources/launchbox/path", new EmulatorPathRequest(root));

            LibrarySyncResultDto? first = await (await client.PostAsync("/api/library/sources/launchbox/sync", null)).Content.ReadFromJsonAsync<LibrarySyncResultDto>();
            LibrarySyncResultDto? second = await (await client.PostAsync("/api/library/sources/launchbox/sync", null)).Content.ReadFromJsonAsync<LibrarySyncResultDto>();
            List<GameDto>? games = await client.GetFromJsonAsync<List<GameDto>>("/api/games");

            first.Should().Be(new LibrarySyncResultDto(3, 3));
            second.Should().Be(new LibrarySyncResultDto(3, 0));
            games.Should().Contain(g => g.Title == "Hades" && g.PlatformName == "Steam");
            games.Should().Contain(g => g.Title == "Assassin's Creed" && g.PlatformName == "Ubisoft");
            games.Should().Contain(g => g.Title == "Onimusha 3" && g.PlatformName == "PS2" && g.PlatformFamily == "PlayStation");
            games.Should().NotContain(g => g.Title == "Hidden");
            Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories).Should().BeEquivalentTo(before);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Tidy_FoldsEmuPlatformsIntoTheRealOne_AndKeepsGamesWithProgress()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        GamerDbContext db = scope.ServiceProvider.GetRequiredService<GamerDbContext>();
        Platform ps2 = db.Platforms.Single(p => p.Name == "PS2");
        Platform emu = new() { Name = "PS2 (Emu)", Family = "Emulador", Format = PlatformFormat.Digital };
        DateTimeOffset now = DateTimeOffset.UtcNow;
        db.Games.AddRange(
            new Game { Title = "Tidy Game", Platform = ps2, Status = GameStatus.Completed, CreatedAt = now, UpdatedAt = now },
            new Game { Title = "Tidy Game", Platform = emu, Status = GameStatus.Backlog, CreatedAt = now, UpdatedAt = now },
            new Game { Title = "Tidy Only Emu", Platform = emu, Status = GameStatus.Backlog, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        await LibraryMaintenance.TidyAsync(db, CancellationToken.None);
        await LibraryMaintenance.TidyAsync(db, CancellationToken.None);

        db.Platforms.Any(p => p.Name == "PS2 (Emu)").Should().BeFalse();
        List<Game> games = db.Games.Include(g => g.Platform).Where(g => g.Title.StartsWith("Tidy")).ToList();
        games.Should().HaveCount(2);
        games.Single(g => g.Title == "Tidy Game").Status.Should().Be(GameStatus.Completed);
        games.Should().OnlyContain(g => g.Platform.Name == "PS2");
    }

    [Fact]
    public async Task Sync_UnknownSource_ReturnsNotFound()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.PostAsync("/api/library/sources/nope/sync", null);

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "secret"));
        LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }
}
