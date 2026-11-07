namespace Gamer.Contracts.Library;

/// <param name="Kind">"account" (online account), "launcher" (games installed by a PC launcher) or "folder".</param>
public record LibrarySourceDto(string Id, string Name, string Kind, string Description, string? Path, bool Available);

public record LibrarySyncResultDto(int Found, int Added);

public record EmulatorPathRequest(string Path);

public record DuplicateGameDto(int Id, string Title, string Platform, Gamer.Contracts.Games.GameStatus Status, bool OnDeck, int? Rating, bool HasNotes, bool Suggested);

public record DuplicateGroupDto(string Platform, IReadOnlyList<DuplicateGameDto> Games);

public record MergeDuplicatesRequest(int KeepId, IReadOnlyList<int> RemoveIds);

public record IgnoreDuplicatesRequest(IReadOnlyList<int> Ids);
