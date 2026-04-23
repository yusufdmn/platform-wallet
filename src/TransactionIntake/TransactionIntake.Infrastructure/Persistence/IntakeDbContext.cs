using MassTransit;
using Microsoft.EntityFrameworkCore;
using PlatformWallet.TransactionIntake.Domain;
using PlatformWallet.TransactionIntake.Infrastructure.Persistence.Outbox;

namespace PlatformWallet.TransactionIntake.Infrastructure.Persistence;

public class IntakeDbContext(DbContextOptions<IntakeDbContext> options) : DbContext(options)
{
    public DbSet<Transaction>    Transactions    => Set<Transaction>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IntakeDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
