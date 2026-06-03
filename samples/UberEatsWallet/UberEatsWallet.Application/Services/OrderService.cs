using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Domain;

namespace UberEatsWallet.Application.Services;

/// <summary>Drives an order through its wallet-backed lifecycle: place (hold) → accept (capture) / reject|cancel (void).</summary>
public sealed class OrderService(
    IWalletGateway gateway,
    ICatalogRepository catalog,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Place an order: hold the cost from the customer toward the restaurant. Returns the new order id.</summary>
    public async Task<Guid> PlaceOrderAsync(Guid customerId, Guid menuItemId, int quantity, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var customer = await catalog.GetCustomerAsync(customerId, ct)
            ?? throw new InvalidOperationException($"Customer {customerId} not found.");
        var item = await catalog.GetMenuItemAsync(menuItemId, ct)
            ?? throw new InvalidOperationException($"Menu item {menuItemId} not found.");
        var restaurant = await catalog.GetRestaurantAsync(item.RestaurantId, ct)
            ?? throw new InvalidOperationException($"Restaurant {item.RestaurantId} not found.");

        var orderId = Guid.NewGuid();
        var amount = item.Price * quantity;

        // Idempotency-Key = our order id, so a double-submitted POST can't double-hold.
        var correlationId = await gateway.TransferAsync(
            customer.WalletAccountId, restaurant.WalletAccountId, amount, orderId.ToString(), ct);

        var order = Order.Place(orderId, customerId, item, quantity, correlationId, clock.UtcNow);
        orders.Add(order);
        await unitOfWork.SaveChangesAsync(ct);

        return orderId;
    }

    /// <summary>Restaurant accepts → capture the held transfer.</summary>
    public async Task AcceptAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, ct);
        await gateway.CaptureAsync(order.OrderTransactionId, ct);
        order.MarkAccepted(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Restaurant rejects → void the hold.</summary>
    public async Task RejectAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, ct);
        await gateway.VoidAsync(order.OrderTransactionId, ct);
        order.MarkRejected(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Customer cancels before acceptance → void the hold.</summary>
    public async Task CancelAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadAsync(orderId, ct);
        await gateway.VoidAsync(order.OrderTransactionId, ct);
        order.MarkCancelled(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Order> LoadAsync(Guid orderId, CancellationToken ct) =>
        await orders.GetAsync(orderId, ct)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");
}
