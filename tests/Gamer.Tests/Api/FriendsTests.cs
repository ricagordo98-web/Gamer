using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Friends;
using Gamer.Api.Features.Steam;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Friends;
using NSubstitute;

namespace Gamer.Tests.Api;

public sealed class FriendsTests(GamerApiFactory factory) : IClassFixture<GamerApiFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    private static SteamPlayer Player(string id, string name, int state = 1, long? gameId = null, string? game = null, DateTimeOffset? lastLogoff = null) =>
        new(id, name, null, null, state, gameId, game, lastLogoff);

    private static FriendsService CreateService(GamerDbContext db, ISteamFriendsClient client, FriendsCache cache, TimeProvider? clock = null)
    {
        ISteamSettingsService settings = Substitute.For<ISteamSettingsService>();
        settings.GetCredentialsAsync(Arg.Any<CancellationToken>()).Returns(new SteamCredentials("key", "7656"));
        return new FriendsService(db, client, settings, cache, clock ?? TimeProvider.System);
    }

    [Fact]
    public async Task Friends_Unauthenticated_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/friends");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Friends_SteamNotConfigured_ReturnsBadRequest()
    {
        HttpClient client = factory.CreateClient();
        LoginResponse? login = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "secret"))).Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);

        HttpResponseMessage response = await client.GetAsync("/api/friends");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void ParseFriendIds_ReadsSteamIds()
    {
        using JsonDocument document = JsonDocument.Parse("""{"friendslist":{"friends":[{"steamid":"1","relationship":"friend"},{"steamid":"2","relationship":"friend"}]}}""");

        SteamFriendsClient.ParseFriendIds(document.RootElement).Should().Equal("1", "2");
    }

    [Fact]
    public void ParsePlayers_ReadsGameAndLastLogoff()
    {
        using JsonDocument document = JsonDocument.Parse("""
            {"response":{"players":[
              {"steamid":"1","personaname":"Ana","avatarfull":"https://a/1.jpg","profileurl":"https://p/1","personastate":1,"gameid":"620","gameextrainfo":"Portal 2","lastlogoff":1700000000},
              {"steamid":"2","personaname":"Rui","personastate":0,"lastlogoff":0}]}}
            """);

        IReadOnlyList<SteamPlayer> players = SteamFriendsClient.ParsePlayers(document.RootElement);

        players[0].Should().Be(new SteamPlayer("1", "Ana", "https://a/1.jpg", "https://p/1", 1, 620, "Portal 2", DateTimeOffset.FromUnixTimeSeconds(1700000000)));
        players[1].GameId.Should().BeNull();
        players[1].LastLogoffAt.Should().BeNull();
    }

    [Fact]
    public async Task Get_OrdersPlayingFirstAndFlagsOwnedGames()
    {
        using TestDatabase database = new();
        using GamerDbContext db = database.CreateContext();
        db.SteamLibrary.Add(new SteamLibraryItem { AppId = 620, Title = "Portal 2" });
        await db.SaveChangesAsync();
        ISteamFriendsClient client = Substitute.For<ISteamFriendsClient>();
        client.GetFriendIdsAsync("key", "7656", Arg.Any<CancellationToken>()).Returns(["1", "2", "3", "4", "5"]);
        client.GetPlayersAsync("key", Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(
        [
            Player("1", "Offline", 0, lastLogoff: Now),
            Player("2", "Away", 3),
            Player("3", "Online", 1),
            Player("4", "Playing", 1, 620, "Portal 2"),
            Player("5", "Busy playing", 2, 999, "Other Game"),
        ]);

        FriendsDto result = await CreateService(db, client, new FriendsCache()).GetAsync(false, CancellationToken.None);

        result.Friends.Select(f => f.Name).Should().Equal("Busy playing", "Playing", "Online", "Away", "Offline");
        result.Friends.Select(f => f.State).Should().Equal(FriendStates.Playing, FriendStates.Playing, FriendStates.Online, FriendStates.Away, FriendStates.Offline);
        result.Friends.Single(f => f.Name == "Playing").OwnsGame.Should().BeTrue();
        result.Friends.Single(f => f.Name == "Busy playing").OwnsGame.Should().BeFalse();
    }

    [Fact]
    public async Task Get_WithinLifetime_UsesCacheUnlessRefreshRequested()
    {
        using TestDatabase database = new();
        using GamerDbContext db = database.CreateContext();
        ISteamFriendsClient client = Substitute.For<ISteamFriendsClient>();
        client.GetFriendIdsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(["1"]);
        client.GetPlayersAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns([Player("1", "Ana")]);
        FriendsService service = CreateService(db, client, new FriendsCache());

        await service.GetAsync(false, CancellationToken.None);
        await service.GetAsync(false, CancellationToken.None);
        await service.GetAsync(true, CancellationToken.None);

        await client.Received(2).GetFriendIdsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Get_NoFriends_ReturnsEmptyWithoutAskingForSummaries()
    {
        using TestDatabase database = new();
        using GamerDbContext db = database.CreateContext();
        ISteamFriendsClient client = Substitute.For<ISteamFriendsClient>();
        client.GetFriendIdsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);

        FriendsDto result = await CreateService(db, client, new FriendsCache()).GetAsync(false, CancellationToken.None);

        result.Friends.Should().BeEmpty();
        await client.DidNotReceive().GetPlayersAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
    }
}
