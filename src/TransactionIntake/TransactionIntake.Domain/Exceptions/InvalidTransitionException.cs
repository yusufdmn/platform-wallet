namespace PlatformWallet.TransactionIntake.Domain.Exceptions;

public sealed class InvalidTransitionException(TransactionStatus from, TransactionStatus to)
    : IntakeDomainException($"Invalid status transition from {from} to {to}.");
