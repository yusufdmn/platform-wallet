namespace PlatformWallet.Ledger.Domain.Exceptions;

public sealed class InvalidAccountIdException : LedgerDomainException
{
    public string RawAccountId { get; }

    public InvalidAccountIdException(string rawAccountId)
        : base($"Invalid account id: '{rawAccountId}'.")
    {
        RawAccountId = rawAccountId;
    }
}
