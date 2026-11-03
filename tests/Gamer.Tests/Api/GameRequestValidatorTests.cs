using FluentAssertions;
using FluentValidation.Results;
using Gamer.Api.Features.Games;
using Gamer.Contracts.Games;

namespace Gamer.Tests.Api;

public sealed class GameRequestValidatorTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private Task<ValidationResult> ValidateAsync(GameRequest request) =>
        new GameRequestValidator(_database.CreateContext()).ValidateAsync(request);

    [Fact]
    public async Task Validate_ValidRequest_IsValid()
    {
        ValidationResult result = await ValidateAsync(new GameRequest("Hades", 12, GameStatus.Playing, true, 8, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyTitle_IsInvalid()
    {
        ValidationResult result = await ValidateAsync(new GameRequest(" ", 12, GameStatus.Backlog, false, null, null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_UnknownPlatform_IsInvalid()
    {
        ValidationResult result = await ValidateAsync(new GameRequest("Hades", 999, GameStatus.Backlog, false, null, null));

        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(GameRequest.PlatformId));
    }

    [Fact]
    public async Task Validate_RatingOutOfRange_IsInvalid()
    {
        ValidationResult result = await ValidateAsync(new GameRequest("Hades", 12, GameStatus.Backlog, false, 11, null));

        result.IsValid.Should().BeFalse();
    }
}
