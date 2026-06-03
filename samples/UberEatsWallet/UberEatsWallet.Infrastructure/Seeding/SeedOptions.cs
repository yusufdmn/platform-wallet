namespace UberEatsWallet.Infrastructure.Seeding;

/// <summary>Demo-seed settings. Bound from the sample's .env.</summary>
public sealed class SeedOptions
{
    /// <summary>Opening balance minted to each seeded customer and restaurant wallet (asset USD).</summary>
    public decimal OpeningBalance { get; set; } = 500m;
}
