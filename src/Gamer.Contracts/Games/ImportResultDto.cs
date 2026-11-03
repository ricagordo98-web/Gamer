namespace Gamer.Contracts.Games;

public record ImportResultDto(int Imported, int Skipped, IReadOnlyList<string> Duplicates);
