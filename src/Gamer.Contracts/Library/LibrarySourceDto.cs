namespace Gamer.Contracts.Library;

/// <param name="Kind">"account" (online account), "launcher" (games installed by a PC launcher) or "folder".</param>
public record LibrarySourceDto(string Id, string Name, string Kind, string Description, string? Path, bool Available);

public record LibrarySyncResultDto(int Found, int Added);

public record EmulatorPathRequest(string Path);
