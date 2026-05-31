using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Infrastructure.Persistence;
using UberEatsWallet.Infrastructure.Wallet;

namespace UberEatsWallet.Infrastructure.Seeding;

/// <summary>
/// Seeds the demo catalog (once) and mints an opening balance to every wallet. Opening mints use a
/// deterministic idempotency key, so repeated startups never double-mint. Mint failures (e.g. the
/// wallet isn't up yet) are logged and skipped rather than crashing startup.
/// </summary>
internal sealed class DemoDataSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<SeedOptions> seedOptions,
    ILogger<DemoDataSeeder> logger) : IHostedService
{
    private const string SeedMintKeyPrefix = "seed-mint-";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gateway = scope.ServiceProvider.GetRequiredService<IWalletGateway>();

        var walletAccountIds = await EnsureCatalogAsync(db, cancellationToken);
        await MintOpeningBalancesAsync(gateway, walletAccountIds, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<IReadOnlyList<Guid>> EnsureCatalogAsync(AppDbContext db, CancellationToken ct)
    {
        var customers = DemoCatalog.CreateCustomers();
        var restaurants = DemoCatalog.CreateRestaurants();

        if (!await db.Customers.AnyAsync(ct))
        {
            db.Customers.AddRange(customers);
            db.Restaurants.AddRange(restaurants);
            db.MenuItems.AddRange(DemoCatalog.CreateMenuItems());
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Seeded demo catalog: {Customers} customers, {Restaurants} restaurants.",
                customers.Count, restaurants.Count);
        }

        return customers.Select(c => c.WalletAccountId)
            .Concat(restaurants.Select(r => r.WalletAccountId))
            .ToList();
    }

    private async Task MintOpeningBalancesAsync(
        IWalletGateway gateway, IReadOnlyList<Guid> walletAccountIds, CancellationToken ct)
    {
        var opening = seedOptions.Value.OpeningBalance;
        if (opening <= 0)
        {
            return;
        }

        foreach (var accountId in walletAccountIds)
        {
            try
            {
                await gateway.MintAsync(accountId, opening, $"{SeedMintKeyPrefix}{accountId}", ct);
            }
            catch (Exception ex) when (ex is WalletGatewayException or HttpRequestException)
            {
                logger.LogWarning(
                    ex, "Opening mint for {AccountId} failed — is the wallet running? Skipping.", accountId);
            }
        }
    }
}
