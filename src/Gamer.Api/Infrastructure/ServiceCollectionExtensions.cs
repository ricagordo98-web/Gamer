using System.Text;
using FluentValidation;
using Gamer.Api.Data;
using Gamer.Api.Features.Cart;
using Gamer.Api.Features.Auth;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Deals.Bundles;
using Gamer.Api.Features.Games;
using Gamer.Api.Features.Import;
using Gamer.Api.Features.Steam;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gamer.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGamerData(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = ResolveConnectionString(configuration.GetConnectionString("Gamer")
            ?? throw new InvalidOperationException("Connection string \"Gamer\" is missing."));

        return services.AddDbContext<GamerDbContext>(options => options.UseSqlite(connectionString));
    }

    public static IServiceCollection AddGamerFeatures(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssemblyContaining<GameRequestValidator>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IExcelImporter, ExcelImporter>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ISteamSettingsService, SteamSettingsService>();
        services.AddScoped<SteamSyncService>();
        services.AddHttpClient<ISteamClient, SteamClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.steampowered.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton(_ => HttpPageFetcher.Create());
        services.AddSingleton<BrowserPageFetcher>();
        services.AddSingleton<IEnumerable<IStoreScraper>>(provider =>
            ScraperCatalog.Create(provider.GetRequiredService<HttpPageFetcher>(), provider.GetRequiredService<BrowserPageFetcher>()));
        services.AddSingleton<StoreSearcher>();
        services.AddScoped<IDealService, DealService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddSingleton<CatalogCache>();
        services.AddSingleton<IEnumerable<IBundleSource>>(provider =>
        [
            new HumbleBundleSource(provider.GetRequiredService<HttpPageFetcher>()),
            new FanaticalBundleSource(provider.GetRequiredService<HttpPageFetcher>()),
        ]);
        services.AddSingleton<BundleCache>();
        services.AddScoped<IBundleService, BundleService>();
        services.AddScoped<ICartService, CartService>();
        services.AddSingleton<PromotionStatusBoard>();
        services.AddSingleton<RefreshCoordinator>();
        return services;
    }

    public static IServiceCollection AddPriceRefresh(this IServiceCollection services, IConfiguration configuration) =>
        configuration.GetValue("Deals:BackgroundRefresh", true) ? services.AddHostedService<PriceRefreshService>() : services;

    public static IServiceCollection AddGamerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, auth) =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = auth.Value.Issuer,
                    ValidAudience = auth.Value.Issuer,
                    IssuerSigningKey = TokenService.CreateKey(auth.Value.JwtKey),
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static string ResolveConnectionString(string connectionString)
    {
        SqliteConnectionStringBuilder builder = new(connectionString);
        string absolutePath = Path.GetFullPath(builder.DataSource, AppContext.BaseDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        builder.DataSource = absolutePath;
        return builder.ToString();
    }
}
