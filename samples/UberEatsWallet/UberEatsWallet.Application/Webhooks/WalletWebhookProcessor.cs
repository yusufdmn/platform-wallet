using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Domain;

namespace UberEatsWallet.Application.Webhooks;

/// <summary>
/// Applies a verified wallet webhook to the matching order. The payload only carries
/// <c>(event_type, correlation_id)</c>, so we look the order up by correlation id and route by
/// which leg it is (the original hold vs. a refund). Transitions are idempotent, so a re-delivered
/// event is harmless.
/// </summary>
public sealed class WalletWebhookProcessor(
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task HandleAsync(string eventType, Guid correlationId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        var order = await orders.GetByTransactionAsync(correlationId, ct);
        if (order is null)
        {
            return; // a mint/burn (top-up/withdraw) or otherwise unrelated transaction
        }

        // The refund leg's capture only confirms a refund we already recorded — nothing to apply.
        var isOrderLeg = order.RefundTransactionId != correlationId;
        if (isOrderLeg && ApplyOrderLegEvent(order, eventType))
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    /// <returns><c>true</c> if the event changed the order and should be persisted.</returns>
    private bool ApplyOrderLegEvent(Order order, string eventType)
    {
        var now = clock.UtcNow;
        switch (eventType)
        {
            case WalletWebhookEvents.Captured:
                order.MarkAccepted(now);
                return true;
            case WalletWebhookEvents.Voided:
                // A void while still Pending means the 5-min TTL auto-voided it (no user reject/cancel).
                order.MarkExpired(now);
                return true;
            case WalletWebhookEvents.Failed:
                order.MarkFailed(now);
                return true;
            default:
                return false; // minted/burned/unknown — not relevant to an order
        }
    }
}
