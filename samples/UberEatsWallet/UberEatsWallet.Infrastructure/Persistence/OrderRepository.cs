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

    public async Task<IReadOnlyList<Order>> GetForCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Order>> GetForRestaurantAsync(Guid restaurantId, CancellationToken ct) =>
        await db.Orders
            .Where(o => o.RestaurantId == restaurantId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

    public void Add(Order order) => db.Orders.Add(order);
}
