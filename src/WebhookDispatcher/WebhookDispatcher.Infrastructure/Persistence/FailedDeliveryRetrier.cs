using Microsoft.EntityFrameworkCore;
using PlatformWallet.WebhookDispatcher.Application.Services;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

internal sealed class FailedDeliveryRetrier(
    WebhookDbContext        db,
    IWebhookDeliveryService delivery) : IFailedDeliveryRetrier
{
    public async Task<RetryOutcome?> RetryAsync(long id, CancellationToken ct)
    {
        var row = await db.FailedDeliveries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            return null;
        }

        row.MarkRetrying();
        await db.SaveChangesAsync(ct);

        try
        {
            await delivery.DeliverAsync(row.EventType, row.CorrelationId, ct);
            row.MarkDelivered(DateTimeOffset.UtcNow);
        }
        catch (WebhookDeliveryException ex)
        {
            row.RecordRetryFailure(DateTimeOffset.UtcNow, ex.Message, ex.StatusCode, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            row.RecordRetryFailure(DateTimeOffset.UtcNow, ex.Message);
        }

        await db.SaveChangesAsync(ct);

        return new RetryOutcome(
            row.Id,
            row.Status.ToString(),
            row.RetryCount,
            row.Status == FailedDeliveryStatus.Delivered);
    }
}
