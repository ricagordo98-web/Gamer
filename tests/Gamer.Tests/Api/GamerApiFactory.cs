using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gamer.Tests.Api;

public sealed class GamerApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"gamer-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Gamer", $"Data Source={_databasePath}");
        builder.UseSetting("Auth:Username", "tester");
        builder.UseSetting("Auth:Password", "secret");
        builder.UseSetting("Deals:BackgroundRefresh", "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
