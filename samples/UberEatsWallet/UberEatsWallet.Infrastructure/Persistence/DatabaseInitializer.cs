using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace UberEatsWallet.Infrastructure.Persistence;

/// <summary>Creates the SQLite schema at startup. Registered before the seeder so the tables exist first.</summary>
internal sealed class DatabaseInitializer(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
