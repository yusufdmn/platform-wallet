namespace PlatformWallet.TransactionIntake.Domain.Exceptions;

public abstract class IntakeDomainException(string reason) : Exception(reason);
