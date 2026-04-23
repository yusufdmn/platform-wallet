using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PlatformWallet.TransactionIntake.Infrastructure.Persistence;

internal sealed class IntakeDbContextFactory : IDesignTimeDbContextFactory<IntakeDbContext>
{
    public IntakeDbContext CreateDbContext(string[] args)
    {
        Env.TraversePath().Load();

        var options = new DbContextOptionsBuilder<IntakeDbContext>()
            .UseNpgsql(BuildConnectionString())
            .Options;

        return new IntakeDbContext(options);
    }

    private static string BuildConnectionString()
    {
        var host     = Environment.GetEnvironmentVariable("POSTGRES_HOST")     ?? "localhost";
        var port     = Environment.GetEnvironmentVariable("POSTGRES_PORT")     ?? "5432";
        var user     = Environment.GetEnvironmentVariable("POSTGRES_USER")     ?? "postgres";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";
        return $"Host={host};Port={port};Database=intake_db;Username={user};Password={password}";
    }
}
