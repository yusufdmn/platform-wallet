namespace PlatformWallet.TransactionIntake.Domain.Exceptions;

public sealed class InvalidAmountException : IntakeDomainException
{
    public decimal Amount { get; }

    public InvalidAmountException(decimal amount)
        : base($"Amount must be positive; got {amount}.")
    {
        Amount = amount;
    }
}
