using Microsoft.EntityFrameworkCore;
using PlatformWallet.Ledger.Domain;
using Xunit;
using PlatformWallet.Ledger.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace PlatformWallet.Ledger.IntegrationTests.Fixtures;

[CollectionDefinition(Name)]
public sealed class LedgerIntegrationGroup : ICollectionFixture<LedgerIntegrationFixture>
{
    public const string Name = "LedgerIntegration";
}

public sealed class LedgerIntegrationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("ledger_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public LedgerDbContext DbContext { get; private set; } = null!;
    public string ConnectionString  { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        DbContext = new LedgerDbContext(options);
        await DbContext.Database.MigrateAsync();

        await SeedSystemAccountsAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private async Task SeedSystemAccountsAsync()
    {
        if (!await DbContext.Accounts.AnyAsync(a => a.Id == SystemAccounts.WorldId))
        {
            DbContext.Accounts.Add(Account.Create(SystemAccounts.WorldId, "@world", "USD", isSystem: true));
        }

        if (!await DbContext.Accounts.AnyAsync(a => a.Id == SystemAccounts.HeldPoolId))
        {
            DbContext.Accounts.Add(Account.Create(SystemAccounts.HeldPoolId, "@held_pool", "USD", isSystem: true));
        }

        await DbContext.SaveChangesAsync();
    }
}
