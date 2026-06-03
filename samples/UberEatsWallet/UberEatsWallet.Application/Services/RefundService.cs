using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Domain;

namespace UberEatsWallet.Application.Services;

/// <summary>
/// Refunds an accepted order via a reverse transfer (restaurant → customer) that is captured immediately.
/// Because the wallet ignores a capture received while the hold is still being processed, we poll the
/// transaction until it reaches <c>Held</c> before capturing.
/// </summary>
public sealed class RefundService(
    IWalletGateway gateway,
    ICatalogRepository catalog,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const string RefundKeyPrefix = "refund-";
    private const string HeldStatus = "Held";
    private const int MaxHeldPollAttempts = 20;
    private static readonly TimeSpan HeldPollInterval = TimeSpan.FromMilliseconds(250);

    public async Task RefundAsync(Guid orderId, CancellationToken ct)
    {
        var order = await orders.GetAsync(orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        if (order.Status != OrderStatus.Accepted)
        {
            throw new InvalidOperationException(
                $"Order {orderId} cannot be refunded from status {order.Status}.");
        }

        var customer = await catalog.GetCustomerAsync(order.CustomerId, ct)
            ?? throw new InvalidOperationException($"Customer {order.CustomerId} not found.");
        var restaurant = await catalog.GetRestaurantAsync(order.RestaurantId, ct)
            ?? throw new InvalidOperationException($"Restaurant {order.RestaurantId} not found.");

        var refundCorrelationId = await gateway.TransferAsync(
            restaurant.WalletAccountId, customer.WalletAccountId, order.Amount, RefundKey(orderId), ct);

        await WaitUntilHeldAsync(refundCorrelationId, ct);
        await gateway.CaptureAsync(refundCorrelationId, ct);

        order.MarkRefunded(refundCorrelationId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static string RefundKey(Guid orderId) => $"{RefundKeyPrefix}{orderId}";

    private async Task WaitUntilHeldAsync(Guid correlationId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxHeldPollAttempts; attempt++)
        {
            var status = await gateway.GetTransactionStatusAsync(correlationId, ct);
            if (string.Equals(status, HeldStatus, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(HeldPollInterval, ct);
        }

        throw new TimeoutException(
            $"Refund transfer {correlationId} did not reach '{HeldStatus}' within the allotted attempts.");
    }
}
