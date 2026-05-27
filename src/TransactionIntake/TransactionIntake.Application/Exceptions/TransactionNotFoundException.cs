namespace PlatformWallet.TransactionIntake.Application.Exceptions;

public sealed class TransactionNotFoundException(Guid id)
    : Exception($"Transaction {id} not found.");
