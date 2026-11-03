using FluentAssertions;
using Gamer.Api.Features.Import;
using Gamer.Contracts.Games;

namespace Gamer.Tests.Api;

public class StatusMapperTests
{
    [Theory]
    [InlineData(false, false, GameStatus.Backlog)]
    [InlineData(true, false, GameStatus.Playing)]
    [InlineData(false, true, GameStatus.Completed)]
    [InlineData(true, true, GameStatus.Completed)]
    public void FromFlags_Combination_ReturnsExpectedStatus(bool playing, bool completed, GameStatus expected)
    {
        StatusMapper.FromFlags(playing, completed).Should().Be(expected);
    }
}
