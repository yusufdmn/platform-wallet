using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

internal sealed class DatabaseMigratorService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigratorService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();

        logger.LogInformation("Applying WebhookDispatcher migrations…");
        await db.Database.MigrateAsync(ct);
        logger.LogInformation("WebhookDispatcher migrations applied.");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
