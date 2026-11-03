using Gamer.Contracts.Games;

namespace Gamer.Api.Features.Import;

public static class StatusMapper
{
    public static GameStatus FromFlags(bool isPlaying, bool isCompleted) =>
        isCompleted ? GameStatus.Completed
        : isPlaying ? GameStatus.Playing
        : GameStatus.Backlog;
}
