using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.SagaOrchestrator.Infrastructure.Persistence;

namespace PlatformWallet.SagaOrchestrator.Infrastructure.HostedServices;

// Auto-voids sagas stuck in Held past the configured TTL by publishing VoidRequested,
// reusing the existing Held → VoidRequested path. Failed auto-voids land in VoidStranded
// where an operator can retry via POST /admin/transactions/{id}/retry-void.
internal sealed class HoldExpiryService(
    IServiceScopeFactory       scopeFactory,
    IConfiguration             configuration,
    ILogger<HoldExpiryService> logger) : BackgroundService
{
    private const string HeldState                  = "Held";
    private const int    DefaultTtlSeconds          = 24 * 60 * 60; // 24h
    private const int    DefaultScanIntervalSeconds = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ttl      = ReadDuration("HoldTtlSeconds",          DefaultTtlSeconds);
        var interval = ReadDuration("HoldExpiryScanIntervalSeconds", DefaultScanIntervalSeconds);

        logger.LogInformation(
            "HoldExpiryService started: ttl={Ttl} interval={Interval}", ttl, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndPublishAsync(ttl, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Hold expiry scan failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ScanAndPublishAsync(TimeSpan ttl, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db      = scope.ServiceProvider.GetRequiredService<SagaDbContext>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var cutoff = DateTimeOffset.UtcNow - ttl;

        var staleIds = await db.TransactionSagaStates
            .AsNoTracking()
            .Where(s => s.CurrentState == HeldState && s.UpdatedAt < cutoff)
            .Select(s => s.CorrelationId)
            .ToListAsync(ct);

        if (staleIds.Count == 0)
        {
            return;
        }

        foreach (var id in staleIds)
        {
            await publish.Publish(new VoidRequested(id), ct);
            logger.LogWarning(
                "Saga {CorrelationId}: hold TTL expired, auto-voiding", id);
        }

        // Persist outbox rows queued by the publishes so they get dispatched.
        await db.SaveChangesAsync(ct);
    }

    private TimeSpan ReadDuration(string key, int defaultSeconds)
    {
        var raw = configuration[key];
        var seconds = int.TryParse(raw, out var parsed) && parsed > 0
            ? parsed
            : defaultSeconds;
        return TimeSpan.FromSeconds(seconds);
    }
}
