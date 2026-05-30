namespace PlatformWallet.Ledger.Domain.Exceptions;

public sealed class InvalidAmountException : LedgerDomainException
{
    public decimal Amount { get; }

    public InvalidAmountException(decimal amount)
        : base($"Amount must be positive; got {amount}.")
    {
        Amount = amount;
    }
}
