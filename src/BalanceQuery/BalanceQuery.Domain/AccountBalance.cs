namespace PlatformWallet.BalanceQuery.Domain;

public sealed record AccountBalance(
    Guid    AccountId,
    string  Asset,
    decimal Balance,
    decimal HeldAmount);
