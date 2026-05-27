namespace PlatformWallet.Ledger.Domain.Exceptions;

public sealed class InsufficientHeldAmountException(Guid accountId, decimal requested, decimal held)
    : LedgerDomainException($"Insufficient held amount on account {accountId}: held={held}, requested={requested}");
