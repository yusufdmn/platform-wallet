using Dapper;
using PlatformWallet.Ledger.Application.Persistence;

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Dapper;

internal sealed class AccountQueries(IDbConnectionFactory connectionFactory) : IAccountQueries
{
    private const string SelectAccountBalanceSql =
        "SELECT id, asset, balance, held_amount AS HeldAmount FROM accounts WHERE id = @Id";

    public async Task<AccountBalanceDto?> GetBalanceAsync(Guid accountId, CancellationToken ct)
    {
        using var conn    = connectionFactory.Create();
        var       command = new CommandDefinition(SelectAccountBalanceSql, new { Id = accountId }, cancellationToken: ct);
        return await conn.QueryFirstOrDefaultAsync<AccountBalanceDto>(command);
    }
}
