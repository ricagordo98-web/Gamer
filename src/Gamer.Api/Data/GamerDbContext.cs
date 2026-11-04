using Gamer.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Gamer.Api.Data;

public class GamerDbContext(DbContextOptions<GamerDbContext> options) : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<SteamLibraryItem> SteamLibrary => Set<SteamLibraryItem>();
    public DbSet<WishlistItem> Wishlist => Set<WishlistItem>();
    public DbSet<TrackedGame> TrackedGames => Set<TrackedGame>();
    public DbSet<PriceSnapshot> PriceSnapshots => Set<PriceSnapshot>();
    public DbSet<Promotion> Promotions => Set<Promotion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Platform>(platform =>
        {
            platform.Property(p => p.Name).HasMaxLength(50);
            platform.Property(p => p.Family).HasMaxLength(50);
            platform.HasIndex(p => p.Name).IsUnique();
            platform.HasData(PlatformCatalog.All);
        });

        modelBuilder.Entity<Game>(game =>
        {
            game.Property(g => g.Title).HasMaxLength(200);
            game.Property(g => g.Notes).HasMaxLength(2000);
            game.HasOne(g => g.Platform).WithMany().HasForeignKey(g => g.PlatformId).OnDelete(DeleteBehavior.Restrict);
            game.HasIndex(g => g.Title);
            game.HasIndex(g => g.Status);
        });

        modelBuilder.Entity<Store>(store =>
        {
            store.Property(s => s.Name).HasMaxLength(50);
            store.Property(s => s.BaseUrl).HasMaxLength(200);
            store.Property(s => s.Region).HasMaxLength(10);
            store.Property(s => s.Currency).HasMaxLength(10);
            store.HasIndex(s => s.Name).IsUnique();
            store.HasData(StoreCatalog.All);
        });

        modelBuilder.Entity<AppSetting>(setting =>
        {
            setting.HasKey(s => s.Key);
            setting.Property(s => s.Key).HasMaxLength(100);
            setting.Property(s => s.Value).HasMaxLength(500);
        });

        modelBuilder.Entity<SteamLibraryItem>(item =>
        {
            item.HasKey(i => i.AppId);
            item.Property(i => i.AppId).ValueGeneratedNever();
            item.Property(i => i.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<WishlistItem>(item =>
        {
            item.HasKey(i => i.AppId);
            item.Property(i => i.AppId).ValueGeneratedNever();
            item.Property(i => i.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<TrackedGame>(tracked =>
        {
            tracked.Property(t => t.Title).HasMaxLength(200);
            tracked.Property(t => t.TitleKey).HasMaxLength(200);
            tracked.HasIndex(t => t.TitleKey).IsUnique();
        });

        modelBuilder.Entity<PriceSnapshot>(snapshot =>
        {
            snapshot.Property(s => s.TitleKey).HasMaxLength(200);
            snapshot.Property(s => s.Reason).HasMaxLength(500);
            snapshot.Property(s => s.OfferTitle).HasMaxLength(300);
            snapshot.Property(s => s.Url).HasMaxLength(1000);
            snapshot.Property(s => s.Currency).HasMaxLength(10);
            snapshot.Property(s => s.ImageUrl).HasMaxLength(1000);
            snapshot.HasOne(s => s.Store).WithMany().HasForeignKey(s => s.StoreId).OnDelete(DeleteBehavior.Cascade);
            snapshot.HasIndex(s => new { s.TitleKey, s.StoreId }).IsUnique();
        });

        modelBuilder.Entity<Promotion>(promotion =>
        {
            promotion.Property(p => p.Title).HasMaxLength(300);
            promotion.Property(p => p.TitleKey).HasMaxLength(300);
            promotion.Property(p => p.ImageUrl).HasMaxLength(1000);
            promotion.Property(p => p.Url).HasMaxLength(1000);
            promotion.Property(p => p.Currency).HasMaxLength(10);
            promotion.HasOne(p => p.Store).WithMany().HasForeignKey(p => p.StoreId).OnDelete(DeleteBehavior.Cascade);
            promotion.HasIndex(p => new { p.StoreId, p.Url }).IsUnique();
            promotion.HasIndex(p => p.TitleKey);
        });
    }
}
