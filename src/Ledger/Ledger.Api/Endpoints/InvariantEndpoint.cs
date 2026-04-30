using Dapper;
using PlatformWallet.Ledger.Application.Persistence;

namespace PlatformWallet.Ledger.Api.Endpoints;

internal static class InvariantEndpoint
{
    private const string Route       = "/admin/invariants/zero-sum";
    private const string AdminPolicy = "ledger:read";

    public static IEndpointRouteBuilder MapInvariantEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(Route, HandleAsync)
            .RequireAuthorization(AdminPolicy);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        IDbConnectionFactory connFactory,
        CancellationToken ct)
    {
        using var conn = connFactory.Create();

        // Returns rows where a (tx_id, phase) pair does not sum to zero.
        const string Sql = """
            SELECT tx_id        AS TxId,
                   phase        AS Phase,
                   SUM(amount_signed) AS NetAmount
            FROM   postings
            GROUP  BY tx_id, phase
            HAVING SUM(amount_signed) <> 0;
            """;

        var violations = (await conn.QueryAsync<ZeroSumViolation>(Sql)).ToList();

        return Results.Ok(new { violations });
    }

    private sealed record ZeroSumViolation(Guid TxId, string Phase, decimal NetAmount);
}
