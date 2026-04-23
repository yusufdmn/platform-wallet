using Dapper;
using PlatformWallet.Ledger.Application.Persistence;

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Dapper;

internal sealed class AccountQueries(IDbConnectionFactory connectionFactory) : IAccountQueries
{
    public async Task<AccountBalanceDto?> GetBalanceAsync(Guid accountId, CancellationToken ct)
    {
        using var conn = connectionFactory.Create();
        return await conn.QueryFirstOrDefaultAsync<AccountBalanceDto>(
            "SELECT id, asset, balance, held_amount AS HeldAmount FROM accounts WHERE id = @Id",
            new { Id = accountId });
    }
}
