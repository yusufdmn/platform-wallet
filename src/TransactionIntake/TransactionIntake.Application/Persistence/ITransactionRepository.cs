using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Persistence;

public interface ITransactionRepository
{
    Task<Transaction?> FindByIdAsync(Guid id, CancellationToken ct);
    void Add(Transaction transaction);
    Task SaveChangesAsync(CancellationToken ct);
}
