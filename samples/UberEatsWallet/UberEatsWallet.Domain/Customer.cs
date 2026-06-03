namespace UberEatsWallet.Domain;

/// <summary>A diner. Maps 1:1 to a ledger account that holds their in-app balance.</summary>
public sealed class Customer
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }

    /// <summary>The ledger account id (created on first mint). Money lives in the wallet, never here.</summary>
    public Guid WalletAccountId { get; private set; }

    private Customer() => Name = string.Empty; // EF materialisation

    public Customer(Guid id, string name, Guid walletAccountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Name = name;
        WalletAccountId = walletAccountId;
    }
}
