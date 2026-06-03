using Microsoft.EntityFrameworkCore;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Domain;

namespace UberEatsWallet.Infrastructure.Persistence;

internal sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<Order?> GetAsync(Guid orderId, CancellationToken ct) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);

    public Task<Order?> GetByTransactionAsync(Guid correlationId, CancellationToken ct) =>
        db.Orders.FirstOrDefaultAsync(
            o => o.OrderTransactionId == correlationId || o.RefundTransactionId == correlationId, ct);

    // SQLite cannot ORDER BY a DateTimeOffset, so we sort newest-first in memory after the query.
    public async Task<IReadOnlyList<Order>> GetForCustomerAsync(Guid customerId, CancellationToken ct)
    {
        var orders = await db.Orders.Where(o => o.CustomerId == customerId).ToListAsync(ct);
        return orders.OrderByDescending(o => o.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<Order>> GetForRestaurantAsync(Guid restaurantId, CancellationToken ct)
    {
        var orders = await db.Orders.Where(o => o.RestaurantId == restaurantId).ToListAsync(ct);
        return orders.OrderByDescending(o => o.CreatedAt).ToList();
    }

    public void Add(Order order) => db.Orders.Add(order);
}
