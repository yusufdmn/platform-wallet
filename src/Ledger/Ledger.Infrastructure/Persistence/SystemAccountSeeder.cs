using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlatformWallet.Ledger.Domain;

namespace PlatformWallet.Ledger.Infrastructure.Persistence;

// Idempotently seeds @world and @held_pool on every startup after migrations run.
internal sealed class SystemAccountSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<SystemAccountSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        await SeedAsync(context, SystemAccounts.WorldId,    "@world",     cancellationToken);
        await SeedAsync(context, SystemAccounts.HeldPoolId, "@held_pool", cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync(LedgerDbContext context, Guid id, string name, CancellationToken ct)
    {
        var exists = await context.Accounts.AnyAsync(a => a.Id == id, ct);
        if (exists)
        {
            return;
        }

        context.Accounts.Add(Account.Create(id, name, "USD", isSystem: true));
        logger.LogInformation("Seeded system account {Name} ({Id})", name, id);
    }
}
