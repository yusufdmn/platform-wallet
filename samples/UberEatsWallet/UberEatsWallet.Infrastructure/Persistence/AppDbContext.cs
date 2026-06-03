using Microsoft.EntityFrameworkCore;
using UberEatsWallet.Domain;

namespace UberEatsWallet.Infrastructure.Persistence;

/// <summary>The app's own store — catalog and orders. Money lives in the ledger, never here.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private const int NameMaxLength = 200;
    private const int StatusMaxLength = 20;
    private const int MoneyPrecision = 18;
    private const int MoneyScale = 2;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(NameMaxLength);
        });

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(NameMaxLength);
            entity.Property(x => x.Cuisine).IsRequired().HasMaxLength(NameMaxLength);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(NameMaxLength);
            entity.Property(x => x.Price).HasPrecision(MoneyPrecision, MoneyScale);
            entity.HasIndex(x => x.RestaurantId);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ItemName).IsRequired().HasMaxLength(NameMaxLength);
            entity.Property(x => x.UnitPrice).HasPrecision(MoneyPrecision, MoneyScale);
            entity.Property(x => x.Amount).HasPrecision(MoneyPrecision, MoneyScale);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(StatusMaxLength);
            entity.HasIndex(x => x.OrderTransactionId);
            entity.HasIndex(x => x.RefundTransactionId);
        });
    }
}
