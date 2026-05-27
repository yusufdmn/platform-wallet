namespace PlatformWallet.Ledger.Domain.Exceptions;

public sealed class AccountNotFoundException(Guid accountId)
    : LedgerDomainException($"Account {accountId} not found.");
