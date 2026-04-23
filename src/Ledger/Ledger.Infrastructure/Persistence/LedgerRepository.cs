using PlatformWallet.Ledger.Application.Persistence;
using PlatformWallet.Ledger.Domain;

namespace PlatformWallet.Ledger.Infrastructure.Persistence;

internal sealed class LedgerRepository(LedgerDbContext context) : ILedgerRepository
{
    public Task<Account?> GetAccountAsync(Guid id, CancellationToken ct) =>
        context.Accounts.FindAsync([id], ct).AsTask();

    public void AddPosting(Posting posting) =>
        context.Postings.Add(posting);

    public Task SaveChangesAsync(CancellationToken ct) =>
        context.SaveChangesAsync(ct);
}
