using Dapper;
using PlatformWallet.Ledger.Application.Persistence;

namespace PlatformWallet.Ledger.Infrastructure.Persistence.Dapper;

internal sealed class AccountQueries(IDbConnectionFactory connectionFactory) : IAccountQueries
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize     = 200;
    private const int MinPage         = 1;

    private const string SelectAccountBalanceSql =
        "SELECT id, asset, balance, held_amount AS HeldAmount FROM accounts WHERE id = @Id";

    private const string CountPostingsSql =
        "SELECT COUNT(*) FROM postings WHERE account_id = @AccountId";

    private const string SelectPostingsPageSql = """
        SELECT id,
               tx_id         AS TxId,
               account_id    AS AccountId,
               asset,
               amount_signed AS AmountSigned,
               entry_kind    AS EntryKind,
               phase,
               created_at    AS CreatedAt
        FROM postings
        WHERE account_id = @AccountId
        ORDER BY created_at DESC, id DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    public async Task<AccountBalanceDto?> GetBalanceAsync(Guid accountId, CancellationToken ct)
    {
        using var conn    = connectionFactory.Create();
        var       command = new CommandDefinition(SelectAccountBalanceSql, new { Id = accountId }, cancellationToken: ct);
        return await conn.QueryFirstOrDefaultAsync<AccountBalanceDto>(command);
    }

    public async Task<PostingHistoryPage> GetPostingsAsync(
        Guid              accountId,
        int               page,
        int               pageSize,
        CancellationToken ct)
    {
        var clampedPage     = page < MinPage ? MinPage : page;
        var clampedPageSize = pageSize switch
        {
            <= 0           => DefaultPageSize,
            > MaxPageSize  => MaxPageSize,
            _              => pageSize,
        };
        var offset = (clampedPage - 1) * clampedPageSize;

        using var conn = connectionFactory.Create();

        var totalCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(CountPostingsSql, new { AccountId = accountId }, cancellationToken: ct));

        var items = (await conn.QueryAsync<PostingHistoryItem>(
            new CommandDefinition(
                SelectPostingsPageSql,
                new { AccountId = accountId, PageSize = clampedPageSize, Offset = offset },
                cancellationToken: ct))).ToList();

        return new PostingHistoryPage(items, totalCount);
    }
}
