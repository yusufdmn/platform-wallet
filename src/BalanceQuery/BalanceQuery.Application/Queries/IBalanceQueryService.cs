using PlatformWallet.BalanceQuery.Domain;

namespace PlatformWallet.BalanceQuery.Application.Queries;

public interface IBalanceQueryService
{
    Task<AccountBalance?> GetBalanceAsync(Guid accountId, CancellationToken ct);
}
