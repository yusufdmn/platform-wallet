using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PlatformWallet.SagaOrchestrator.Domain;

namespace PlatformWallet.SagaOrchestrator.Infrastructure.Persistence;

public class SagaDbContext(DbContextOptions<SagaDbContext> options) : DbContext(options)
{
    public DbSet<TransactionSagaState> TransactionSagaStates { get; init; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SagaDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampSagaRowVersions();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampSagaRowVersions();
        return base.SaveChanges();
    }

    private void StampSagaRowVersions()
    {
        foreach (EntityEntry<TransactionSagaState> entry in ChangeTracker.Entries<TransactionSagaState>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.RowVersion = NewId.NextGuid().ToByteArray();
            }
        }
    }
}
