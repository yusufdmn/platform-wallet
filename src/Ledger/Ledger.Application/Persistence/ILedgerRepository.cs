using PlatformWallet.Ledger.Domain;

namespace PlatformWallet.Ledger.Application.Persistence;

public interface ILedgerRepository
{
    Task<Account?> GetAccountAsync(Guid id, CancellationToken ct);
    void AddAccount(Account account);
    void AddPosting(Posting posting);
    Task SaveChangesAsync(CancellationToken ct);
}
