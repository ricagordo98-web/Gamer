using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Gamer.Contracts.Auth;
using Gamer.Contracts.Games;

namespace Gamer.Tests.Api;

public sealed class ApiEndpointsTests : IClassFixture<GamerApiFactory>
{
    private readonly GamerApiFactory _factory;

    public ApiEndpointsTests(GamerApiFactory factory) => _factory = factory;

    private async Task<HttpClient> CreateAuthorizedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "secret"));
        LoginResponse? login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("tester", "wrong"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetGames_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/api/games");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPlatforms_Authorized_ReturnsSeededPlatforms()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        List<PlatformDto>? platforms = await client.GetFromJsonAsync<List<PlatformDto>>("/api/platforms");

        platforms.Should().HaveCount(16);
    }

    [Fact]
    public async Task GameLifecycle_CreateChangeStatusDelete_Works()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/games", new GameRequest("Hades", 12, GameStatus.Backlog, true, null, null));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        GameDto game = (await created.Content.ReadFromJsonAsync<GameDto>())!;

        HttpResponseMessage patched = await client.PatchAsJsonAsync($"/api/games/{game.Id}/status", new StatusRequest(GameStatus.Completed));
        (await patched.Content.ReadFromJsonAsync<GameDto>())!.Status.Should().Be(GameStatus.Completed);

        (await client.DeleteAsync($"/api/games/{game.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/games/{game.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateGame_InvalidRequest_ReturnsValidationProblem()
    {
        HttpClient client = await CreateAuthorizedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/games", new GameRequest(string.Empty, 12, GameStatus.Backlog, false, null, null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
