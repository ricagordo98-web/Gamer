using FluentAssertions;
using Gamer.Api.Data;
using Gamer.Api.Domain;
using Gamer.Api.Features.Library;
using Gamer.Contracts.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gamer.Tests.Api;

public sealed class DuplicateTests : IClassFixture<GamerApiFactory>
{
    private readonly GamerApiFactory _factory;

    public DuplicateTests(GamerApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("COD 3", "Call of Duty 3")]
    [InlineData("Cod Finest Hour", "Call of Duty - Finest Hour")]
    [InlineData("COD WaW Final Fronts", "Call of Duty - World at War - Final Fronts")]
    [InlineData("God of War II", "God of War 2")]
    [InlineData("Ratchet & Clank", "Ratchet and Clank")]
    [InlineData("Fifth Element, The", "The Fifth Element")]
    [InlineData("Luigi's Mansion", "Luigis Mansion")]
    [InlineData("S.T.A.L.K.E.R.: Shadow of Chernobyl", "STALKER Shadow Of Chernobyl")]
    [InlineData("Pizza Hut Demo 01 (France)", "Pizza Hut Demo 01")]
    public void Create_SameGameDifferentSpelling_GivesTheSameKey(string left, string right) =>
        GameTitleKey.Create(left).Should().Be(GameTitleKey.Create(right));

    [Theory]
    [InlineData("Call of Duty 3", "Call of Duty 4")]
    [InlineData("FIFA 14", "FIFA 15")]
    [InlineData("Mega Man X3", "Mega Man X4")]
    [InlineData("GRID", "GRID (2019)")]
    public void Create_DifferentGames_GiveDifferentKeys(string left, string right) =>
        GameTitleKey.Create(left).Should().NotBe(GameTitleKey.Create(right));

    [Fact]
    public void Find_TyposAreLikely_ButSequelsAreNot()
    {
        List<Game> games =
        [
            Game(1, "007 - Nightfire"), Game(2, "007 Nigthfire"), Game(3, "Mega Man X3"), Game(4, "Mega Man X4"), Game(5, "FIFA 14"), Game(6, "FIFA 15"),
        ];

        IReadOnlyList<DuplicateCluster> clusters = DuplicateFinder.Find(games);

        DuplicateCluster cluster = clusters.Should().ContainSingle().Subject;
        cluster.Certain.Should().BeFalse();
        cluster.Games.Select(g => g.Id).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task AutoMerge_KeepsTheCompletedCopy_AndTheFullerTitle()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        GamerDbContext db = scope.ServiceProvider.GetRequiredService<GamerDbContext>();
        Platform ps2 = db.Platforms.Single(p => p.Name == "PS2");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        db.Games.AddRange(
            new Game { Title = "DupTest COD 3", Platform = ps2, Status = GameStatus.Backlog, Notes = "from excel", CreatedAt = now, UpdatedAt = now },
            new Game { Title = "DupTest Call of Duty 3", Platform = ps2, Status = GameStatus.Completed, Rating = 4, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();

        await DuplicateService.AutoMergeAsync(db, CancellationToken.None);

        Game kept = (await db.Games.Where(g => g.Title.StartsWith("DupTest")).ToListAsync()).Should().ContainSingle().Subject;
        kept.Status.Should().Be(GameStatus.Completed);
        kept.Title.Should().Be("DupTest Call of Duty 3");
        kept.Rating.Should().Be(4);
        kept.Notes.Should().Be("from excel");
    }

    private static Game Game(int id, string title) => new() { Id = id, Title = title, PlatformId = 2, Platform = new Platform { Name = "PS2", Family = "PlayStation" } };
}
