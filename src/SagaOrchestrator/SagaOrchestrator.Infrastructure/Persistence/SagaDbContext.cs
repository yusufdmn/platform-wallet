using MassTransit;
using Microsoft.EntityFrameworkCore;
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
}
