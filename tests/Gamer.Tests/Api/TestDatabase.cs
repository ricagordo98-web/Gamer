using Gamer.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Tests.Api;

public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        using GamerDbContext context = CreateContext();
        context.Database.EnsureCreated();
    }

    public GamerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GamerDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
