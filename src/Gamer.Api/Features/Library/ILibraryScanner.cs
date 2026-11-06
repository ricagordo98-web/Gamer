namespace Gamer.Api.Features.Library;

public record ScannedGame(string Title, string PlatformName, string PlatformFamily);

public interface ILibraryScanner
{
    string Id { get; }
    string Name { get; }
    string Kind { get; }
    string Description { get; }

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
    Task<string?> GetPathAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ScannedGame>> ScanAsync(CancellationToken cancellationToken);
}

public class LibraryScanException(string message) : Exception(message);
