namespace PlatformWallet.Ledger.Domain.Exceptions;

public sealed class InsufficientFundsException(Guid accountId, decimal requested, decimal available)
    : LedgerDomainException($"Insufficient balance on account {accountId}: balance={available}, requested={requested}");
