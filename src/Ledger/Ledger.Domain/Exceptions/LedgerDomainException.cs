namespace PlatformWallet.Ledger.Domain.Exceptions;

public abstract class LedgerDomainException(string reason) : Exception(reason);
