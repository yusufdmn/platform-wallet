namespace PlatformWallet.TransactionIntake.Domain.Exceptions;

public sealed class TransactionNotFoundException(Guid id)
    : IntakeDomainException($"Transaction {id} not found.");
