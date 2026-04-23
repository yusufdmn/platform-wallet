using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PlatformWallet.Ledger.Infrastructure.Persistence;

// Used only by `dotnet ef` tooling at design time — not registered in DI.
internal sealed class LedgerDbContextFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    public LedgerDbContext CreateDbContext(string[] args)
    {
        DotNetEnv.Env.TraversePath().Load();

        var host     = Environment.GetEnvironmentVariable("POSTGRES_HOST")     ?? "localhost";
        var port     = Environment.GetEnvironmentVariable("POSTGRES_PORT")     ?? "5432";
        var user     = Environment.GetEnvironmentVariable("POSTGRES_USER")     ?? "postgres";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? string.Empty;
        var connStr  = $"Host={host};Port={port};Database=ledger_db;Username={user};Password={password}";

        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(connStr)
            .Options;

        return new LedgerDbContext(options);
    }
}
