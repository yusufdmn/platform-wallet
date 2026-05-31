using UberEatsWallet.Domain;

namespace UberEatsWallet.Application.Abstractions;

/// <summary>Read access to the seeded demo catalog: customers, restaurants, and menus.</summary>
public interface ICatalogRepository
{
    Task<IReadOnlyList<Customer>> GetCustomersAsync(CancellationToken ct);

    Task<Customer?> GetCustomerAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Restaurant>> GetRestaurantsAsync(CancellationToken ct);

    Task<Restaurant?> GetRestaurantAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<MenuItem>> GetMenuAsync(Guid restaurantId, CancellationToken ct);

    Task<MenuItem?> GetMenuItemAsync(Guid id, CancellationToken ct);
}
