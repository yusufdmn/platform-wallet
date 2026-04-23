using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Infrastructure.Persistence;

internal sealed class TransactionRepository(IntakeDbContext context) : ITransactionRepository
{
    public Task<Transaction?> FindByIdAsync(Guid id, CancellationToken ct) =>
        context.Transactions.FindAsync([id], ct).AsTask();

    public void Add(Transaction transaction) =>
        context.Transactions.Add(transaction);

    public Task SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
