using UberEatsWallet.Domain;

namespace UberEatsWallet.Infrastructure.Seeding;

/// <summary>
/// Fixed demo data. Ids (and wallet account ids) are stable across DB resets so the same ledger
/// accounts are reused. Prices are fixed-but-varied (no randomness needed for the demo).
/// </summary>
internal static class DemoCatalog
{
    private static readonly Guid BosphorusId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private static readonly Guid SakuraId = Guid.Parse("d0000000-0000-0000-0000-000000000002");
    private static readonly Guid TrattoriaId = Guid.Parse("d0000000-0000-0000-0000-000000000003");
    private static readonly Guid TacoId = Guid.Parse("d0000000-0000-0000-0000-000000000004");

    public static IReadOnlyList<Customer> CreateCustomers() =>
    [
        new(Guid.Parse("c0000000-0000-0000-0000-000000000001"), "Ada Lovelace", Guid.Parse("a0000000-0000-0000-0000-000000000001")),
        new(Guid.Parse("c0000000-0000-0000-0000-000000000002"), "Alan Turing", Guid.Parse("a0000000-0000-0000-0000-000000000002")),
        new(Guid.Parse("c0000000-0000-0000-0000-000000000003"), "Grace Hopper", Guid.Parse("a0000000-0000-0000-0000-000000000003")),
        new(Guid.Parse("c0000000-0000-0000-0000-000000000004"), "Linus Torvalds", Guid.Parse("a0000000-0000-0000-0000-000000000004")),
    ];

    public static IReadOnlyList<Restaurant> CreateRestaurants() =>
    [
        new(BosphorusId, "Bosphorus Grill", "Turkish", Guid.Parse("b0000000-0000-0000-0000-000000000001")),
        new(SakuraId, "Sakura Sushi", "Japanese", Guid.Parse("b0000000-0000-0000-0000-000000000002")),
        new(TrattoriaId, "Trattoria Roma", "Italian", Guid.Parse("b0000000-0000-0000-0000-000000000003")),
        new(TacoId, "Taco Fiesta", "Mexican", Guid.Parse("b0000000-0000-0000-0000-000000000004")),
    ];

    public static IReadOnlyList<MenuItem> CreateMenuItems() =>
    [
        new(Guid.Parse("e0000000-0000-0000-0000-000000000001"), BosphorusId, "Adana Kebab", 14.50m),
        new(Guid.Parse("e0000000-0000-0000-0000-000000000002"), BosphorusId, "Lahmacun", 8.75m),
        new(Guid.Parse("e0000000-0000-0000-0000-000000000003"), BosphorusId, "Baklava", 6.25m),

        new(Guid.Parse("e0000000-0000-0000-0000-000000000004"), SakuraId, "Salmon Nigiri", 12.00m),
        new(Guid.Parse("e0000000-0000-0000-0000-000000000005"), SakuraId, "Dragon Roll", 16.50m),
        new(Guid.Parse("e0000000-0000-0000-0000-000000000006"), SakuraId, "Miso Soup", 4.50m),

        new(Guid.Parse("e0000000-0000-0000-0000-000000000007"), TrattoriaId, "Margherita Pizza", 13.25m),
        new(Guid.Parse("e0000000-0000-0000-0000-000000000008"), TrattoriaId, "Carbonara", 15.75m),
        new(Guid.Parse("e0000000-0000-0000-0000-000000000009"), TrattoriaId, "Tiramisu", 7.00m),

        new(Guid.Parse("e0000000-0000-0000-0000-000000000010"), TacoId, "Al Pastor Tacos", 11.50m),
        new(Guid.Parse("e0000000-0000-0000-0000-000000000011"), TacoId, "Burrito Bowl", 12.75m),
        new(Guid.Parse("e0000000-0000-0000-0000-000000000012"), TacoId, "Churros", 5.50m),
    ];
}
