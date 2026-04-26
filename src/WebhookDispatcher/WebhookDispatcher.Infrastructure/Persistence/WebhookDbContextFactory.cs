using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

internal sealed class WebhookDbContextFactory : IDesignTimeDbContextFactory<WebhookDbContext>
{
    public WebhookDbContext CreateDbContext(string[] args)
    {
        DotNetEnv.Env.TraversePath().Load();

        var host     = Environment.GetEnvironmentVariable("POSTGRES_HOST")     ?? "localhost";
        var port     = Environment.GetEnvironmentVariable("POSTGRES_PORT")     ?? "5432";
        var user     = Environment.GetEnvironmentVariable("POSTGRES_USER")     ?? "postgres";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? string.Empty;

        var connectionString = $"Host={host};Port={port};Database=webhook_db;Username={user};Password={password}";

        var opts = new DbContextOptionsBuilder<WebhookDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WebhookDbContext(opts);
    }
}
