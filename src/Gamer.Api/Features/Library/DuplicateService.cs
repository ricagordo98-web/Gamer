using System.Text.Json;
using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Contracts.Games;
using Gamer.Contracts.Library;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Features.Library;

public record DuplicateCluster(bool Certain, IReadOnlyList<Game> Games);

public static class DuplicateFinder
{
    public const double LikelyThreshold = 0.86;
    private const int MinimumKeyLength = 5;

    /// <summary>Groups games of the same platform that look like the same game. Certain = identical canonical title.</summary>
    public static IReadOnlyList<DuplicateCluster> Find(IEnumerable<Game> games)
    {
        List<DuplicateCluster> clusters = [];
        foreach (IGrouping<int, Game> platform in games.GroupBy(g => g.PlatformId))
        {
            List<(string Key, List<Game> Games)> byKey = platform
                .GroupBy(g => GameTitleKey.Create(g.Title))
                .Where(g => g.Key.Length > 0)
                .Select(g => (g.Key, g.ToList()))
                .ToList();

            foreach ((string _, List<Game> same) in byKey.Where(k => k.Games.Count > 1))
            {
                clusters.Add(new DuplicateCluster(true, same));
            }

            clusters.AddRange(FindLikely(byKey));
        }

        return clusters;
    }

    private static IEnumerable<DuplicateCluster> FindLikely(List<(string Key, List<Game> Games)> byKey)
    {
        int count = byKey.Count;
        int[] parent = Enumerable.Range(0, count).ToArray();
        string[] numbers = byKey.Select(k => GameTitleKey.Numbers(k.Key)).ToArray();

        int Root(int i) => parent[i] == i ? i : parent[i] = Root(parent[i]);

        for (int i = 0; i < count; i++)
        {
            string left = byKey[i].Key;
            if (left.Length < MinimumKeyLength)
            {
                continue;
            }

            for (int j = i + 1; j < count; j++)
            {
                string right = byKey[j].Key;
                if (right.Length < MinimumKeyLength || numbers[i] != numbers[j]
                    || Math.Abs(left.Length - right.Length) > 0.14 * Math.Max(left.Length, right.Length)
                    || GameTitleKey.Similarity(left, right) < LikelyThreshold)
                {
                    continue;
                }

                parent[Root(j)] = Root(i);
            }
        }

        return Enumerable.Range(0, count)
            .GroupBy(Root)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateCluster(false, g.SelectMany(i => byKey[i].Games).ToList()));
    }
}

public class DuplicateService(GamerDbContext db, TimeProvider clock)
{
    public const string IgnoreKey = "library.duplicates.ignored";

    public static async Task<int> AutoMergeAsync(GamerDbContext db, CancellationToken cancellationToken)
    {
        List<Game> games = await db.Games.ToListAsync(cancellationToken);
        int merged = 0;
        foreach (DuplicateCluster cluster in DuplicateFinder.Find(games).Where(c => c.Certain))
        {
            merged += Merge(db, cluster.Games, Suggested(cluster.Games));
        }

        if (merged > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return merged;
    }

    public async Task<IReadOnlyList<DuplicateGroupDto>> ListAsync(CancellationToken cancellationToken)
    {
        List<Game> games = await db.Games.AsNoTracking().Include(g => g.Platform).ToListAsync(cancellationToken);
        HashSet<string> ignored = await LoadIgnoredAsync(cancellationToken);

        return DuplicateFinder.Find(games)
            .Where(c => !c.Certain && !ignored.Contains(GroupKey(c.Games)))
            .Select(c =>
            {
                Game suggested = Suggested(c.Games);
                return new DuplicateGroupDto(
                    c.Games[0].Platform.Name,
                    c.Games
                        .OrderByDescending(g => g.Id == suggested.Id)
                        .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                        .Select(g => new DuplicateGameDto(g.Id, g.Title, g.Platform.Name, g.Status, g.OnDeck, g.Rating, !string.IsNullOrWhiteSpace(g.Notes), g.Id == suggested.Id))
                        .ToList());
            })
            .OrderBy(g => g.Platform)
            .ThenBy(g => g.Games[0].Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<int> MergeAsync(int keepId, IReadOnlyList<int> removeIds, CancellationToken cancellationToken)
    {
        List<int> ids = [keepId, .. removeIds.Where(id => id != keepId)];
        List<Game> games = await db.Games.Where(g => ids.Contains(g.Id)).ToListAsync(cancellationToken);
        Game? keeper = games.FirstOrDefault(g => g.Id == keepId);
        if (keeper is null || games.Any(g => g.PlatformId != keeper.PlatformId))
        {
            return 0;
        }

        int removed = Merge(db, games, keeper);
        keeper.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return removed;
    }

    public async Task IgnoreAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        HashSet<string> ignored = await LoadIgnoredAsync(cancellationToken);
        ignored.Add(GroupKey(ids));
        string json = JsonSerializer.Serialize(ignored);
        AppSetting? setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == IgnoreKey, cancellationToken);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = IgnoreKey, Value = json });
        }
        else
        {
            setting.Value = json;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The copy to keep: completed first, then playing, then the one with the most information, then the oldest.</summary>
    private static Game Suggested(IReadOnlyList<Game> games) =>
        games.OrderByDescending(g => (int)g.Status)
            .ThenByDescending(g => (g.OnDeck ? 1 : 0) + (g.Rating is null ? 0 : 1) + (string.IsNullOrWhiteSpace(g.Notes) ? 0 : 1))
            .ThenBy(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .First();

    // Folds every copy into the keeper (best status wins, nothing is lost) and deletes the others. Returns how many were removed.
    private static int Merge(GamerDbContext db, IReadOnlyList<Game> games, Game keeper)
    {
        List<Game> others = games.Where(g => g.Id != keeper.Id).ToList();
        foreach (Game other in others)
        {
            keeper.Status = (GameStatus)Math.Max((int)keeper.Status, (int)other.Status);
            keeper.OnDeck |= other.OnDeck;
            keeper.Rating = keeper.Rating is null ? other.Rating : other.Rating is null ? keeper.Rating : Math.Max(keeper.Rating.Value, other.Rating.Value);
            if (!string.IsNullOrWhiteSpace(other.Notes) && keeper.Notes?.Contains(other.Notes.Trim(), StringComparison.OrdinalIgnoreCase) != true)
            {
                keeper.Notes = string.IsNullOrWhiteSpace(keeper.Notes) ? other.Notes : $"{keeper.Notes}\n{other.Notes}";
            }

            if (other.Title.Length > keeper.Title.Length)
            {
                keeper.Title = other.Title;
            }

            db.Games.Remove(other);
        }

        return others.Count;
    }

    private async Task<HashSet<string>> LoadIgnoredAsync(CancellationToken cancellationToken)
    {
        string? json = await db.AppSettings.AsNoTracking().Where(s => s.Key == IgnoreKey).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken);
        try
        {
            return json is null ? [] : JsonSerializer.Deserialize<HashSet<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string GroupKey(IEnumerable<Game> games) => GroupKey(games.Select(g => g.Id));

    private static string GroupKey(IEnumerable<int> ids) => string.Join(',', ids.Order());
}
