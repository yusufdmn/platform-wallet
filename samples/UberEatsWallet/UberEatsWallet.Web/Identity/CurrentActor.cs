namespace UberEatsWallet.Web.Identity;

/// <summary>The seeded customer or restaurant the session is "acting as" (a demo shim, not real auth).</summary>
public sealed record CurrentActor(ActorType Type, Guid Id, string Name, Guid WalletAccountId)
{
    public bool IsCustomer => Type == ActorType.Customer;
    public bool IsRestaurant => Type == ActorType.Restaurant;
}
