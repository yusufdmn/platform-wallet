using Microsoft.EntityFrameworkCore;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

public sealed class WebhookDbContext(DbContextOptions<WebhookDbContext> options) : DbContext(options)
{
    public DbSet<FailedWebhookDelivery> FailedDeliveries => Set<FailedWebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WebhookDbContext).Assembly);
    }
}
