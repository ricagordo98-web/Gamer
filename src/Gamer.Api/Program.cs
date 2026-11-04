using Microsoft.AspNetCore.StaticFiles;
using Gamer.Api.Data;
using Gamer.Api.Features.Auth;
using Gamer.Api.Features.Deals;
using Gamer.Api.Features.Games;
using Gamer.Api.Features.Import;
using Gamer.Api.Features.Platforms;
using Gamer.Api.Features.Steam;
using Gamer.Api.Features.Stores;
using Gamer.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.AddGamerData(builder.Configuration);
builder.Services.AddGamerFeatures();
builder.Services.AddPriceRefresh(builder.Configuration);
builder.Services.AddGamerAuthentication(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddCors(cors => cors.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

WebApplication app = builder.Build();

await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<GamerDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = CreateContentTypeProvider() });
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());
app.MapAuthEndpoints();
app.MapPlatformEndpoints();
app.MapGameEndpoints();
app.MapImportEndpoints();
app.MapStoreEndpoints();
app.MapSteamEndpoints();
app.MapDealEndpoints();
app.MapPromotionEndpoints();
app.MapFallbackToFile("index.html");

await app.RunAsync();

static FileExtensionContentTypeProvider CreateContentTypeProvider()
{
    FileExtensionContentTypeProvider provider = new();
    provider.Mappings[".dat"] = "application/octet-stream";
    return provider;
}

public partial class Program;

