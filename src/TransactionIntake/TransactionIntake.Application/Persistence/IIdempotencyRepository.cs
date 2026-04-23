namespace PlatformWallet.TransactionIntake.Application.Persistence;

public interface IIdempotencyRepository
{
    Task<Guid?> FindTransactionIdAsync(string keyHash, CancellationToken ct);
    void Add(string keyHash, Guid transactionId);
}
