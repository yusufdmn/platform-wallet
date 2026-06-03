using UberEatsWallet.Domain;

namespace UberEatsWallet.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetAsync(Guid orderId, CancellationToken ct);

    /// <summary>Finds the order whose order-leg or refund-leg correlation id matches (for webhook routing).</summary>
    Task<Order?> GetByTransactionAsync(Guid correlationId, CancellationToken ct);

    Task<IReadOnlyList<Order>> GetForCustomerAsync(Guid customerId, CancellationToken ct);

    Task<IReadOnlyList<Order>> GetForRestaurantAsync(Guid restaurantId, CancellationToken ct);

    void Add(Order order);
}
