using Microsoft.EntityFrameworkCore;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Domain;

namespace UberEatsWallet.Infrastructure.Persistence;

internal sealed class CatalogRepository(AppDbContext db) : ICatalogRepository
{
    public async Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken ct) =>
        await db.Customers.OrderBy(c => c.Name).ToListAsync(ct);

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken ct) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Restaurant>> GetRestaurantsAsync(CancellationToken ct) =>
        await db.Restaurants.OrderBy(r => r.Name).ToListAsync(ct);

    public Task<Restaurant?> GetRestaurantAsync(Guid id, CancellationToken ct) =>
        db.Restaurants.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<MenuItem>> GetMenuAsync(Guid restaurantId, CancellationToken ct) =>
        await db.MenuItems.Where(m => m.RestaurantId == restaurantId).OrderBy(m => m.Name).ToListAsync(ct);

    public Task<MenuItem?> GetMenuItemAsync(Guid id, CancellationToken ct) =>
        db.MenuItems.FirstOrDefaultAsync(m => m.Id == id, ct);
}
