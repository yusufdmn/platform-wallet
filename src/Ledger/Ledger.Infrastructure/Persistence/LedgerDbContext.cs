using MassTransit;
using Microsoft.EntityFrameworkCore;
using PlatformWallet.Ledger.Domain;

namespace PlatformWallet.Ledger.Infrastructure.Persistence;

public class LedgerDbContext(DbContextOptions<LedgerDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts { get; init; } = null!;
    public DbSet<Posting> Postings { get; init; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LedgerDbContext).Assembly);
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
    }
}
