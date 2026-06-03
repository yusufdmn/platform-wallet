namespace UberEatsWallet.Domain;

/// <summary>A restaurant. Maps 1:1 to a ledger account that receives captured order funds.</summary>
public sealed class Restaurant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Cuisine { get; private set; }
    public Guid WalletAccountId { get; private set; }

    private Restaurant()
    {
        Name = string.Empty;
        Cuisine = string.Empty;
    }

    public Restaurant(Guid id, string name, string cuisine, Guid walletAccountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(cuisine);
        Id = id;
        Name = name;
        Cuisine = cuisine;
        WalletAccountId = walletAccountId;
    }
}
