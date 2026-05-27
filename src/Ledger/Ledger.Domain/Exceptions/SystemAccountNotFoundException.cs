namespace PlatformWallet.Ledger.Domain.Exceptions;

public sealed class SystemAccountNotFoundException(string systemAccountName)
    : LedgerDomainException($"System account '{systemAccountName}' not found — run migrations.");
