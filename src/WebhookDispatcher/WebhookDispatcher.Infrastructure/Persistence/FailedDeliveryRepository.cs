using PlatformWallet.WebhookDispatcher.Application.Services;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

internal sealed class FailedDeliveryRepository(WebhookDbContext db) : IFailedDeliveryRepository
{
    public async Task PersistAsync(
        string eventType,
        Guid correlationId,
        string reason,
        int? lastHttpStatusCode,
        string? lastHttpResponseBody,
        CancellationToken ct)
    {
        db.FailedDeliveries.Add(
            FailedWebhookDelivery.Create(eventType, correlationId, reason, lastHttpStatusCode, lastHttpResponseBody));
        await db.SaveChangesAsync(ct);
    }
}
