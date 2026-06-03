namespace UberEatsWallet.Domain;

/// <summary>A purchasable dish belonging to a restaurant. Price is denominated in the wallet asset (USD).</summary>
public sealed class MenuItem
{
    public Guid Id { get; private set; }
    public Guid RestaurantId { get; private set; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }

    private MenuItem() => Name = string.Empty; // EF materialisation

    public MenuItem(Guid id, Guid restaurantId, string name, decimal price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        Id = id;
        RestaurantId = restaurantId;
        Name = name;
        Price = price;
    }
}
