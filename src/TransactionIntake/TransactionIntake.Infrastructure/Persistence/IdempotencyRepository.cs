using Microsoft.EntityFrameworkCore;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Infrastructure.Persistence.Outbox;

namespace PlatformWallet.TransactionIntake.Infrastructure.Persistence;

internal sealed class IdempotencyRepository(IntakeDbContext context) : IIdempotencyRepository
{
    public async Task<Guid?> FindTransactionIdAsync(string keyHash, CancellationToken ct)
    {
        var entry = await context.IdempotencyKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);

        return entry?.TransactionId;
    }

    public void Add(string keyHash, Guid transactionId) =>
        context.IdempotencyKeys.Add(IdempotencyKey.Create(keyHash, transactionId));
}
