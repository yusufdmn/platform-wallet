using Dapper;
using Microsoft.AspNetCore.Mvc;
using PlatformWallet.SagaOrchestrator.Infrastructure.Persistence;

namespace PlatformWallet.SagaOrchestrator.Worker.Endpoints;

public static class SagaAdminEndpoints
{
    private const string AdminPolicy = "ledger:admin";
    private const int    DefaultTake = 25;
    private const int    MaxTake     = 100;

    public static void MapSagaAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/admin/sagas").RequireAuthorization(AdminPolicy);

        group.MapGet("/",                       ListAsync);
        group.MapGet("/{correlationId:guid}",   GetAsync);
    }

    private static async Task<IResult> ListAsync(
        [FromQuery] string?      state,
        [FromQuery] int?         take,
        [FromQuery] int?         skip,
        ISagaConnectionFactory   conns,
        CancellationToken        ct)
    {
        var limit  = ClampTake(take);
        var offset = skip is > 0 ? skip.Value : 0;

        const string Sql = """
            SELECT correlation_id    AS CorrelationId,
                   current_state     AS CurrentState,
                   transaction_type  AS TransactionType,
                   debit_account_id  AS DebitAccountId,
                   credit_account_id AS CreditAccountId,
                   amount            AS Amount,
                   asset             AS Asset,
                   created_at        AS CreatedAt,
                   updated_at        AS UpdatedAt
            FROM   transaction_saga_states
            WHERE  (@state IS NULL OR current_state = @state)
            ORDER  BY created_at DESC
            LIMIT  @limit
            OFFSET @offset;
            """;

        using var conn = conns.Create();
        var rows = await conn.QueryAsync<SagaListRow>(
            new CommandDefinition(Sql, new { state, limit, offset }, cancellationToken: ct));

        return Results.Ok(new { items = rows, take = limit, skip = offset });
    }

    private static async Task<IResult> GetAsync(
        Guid                   correlationId,
        ISagaConnectionFactory conns,
        CancellationToken      ct)
    {
        const string Sql = """
            SELECT correlation_id      AS CorrelationId,
                   current_state       AS CurrentState,
                   transaction_type    AS TransactionType,
                   debit_account_id    AS DebitAccountId,
                   credit_account_id   AS CreditAccountId,
                   amount              AS Amount,
                   asset               AS Asset,
                   failure_reason      AS FailureReason,
                   void_attempts       AS VoidAttempts,
                   hold_expiry_token_id AS HoldExpiryTokenId,
                   created_at          AS CreatedAt,
                   updated_at          AS UpdatedAt
            FROM   transaction_saga_states
            WHERE  correlation_id = @correlationId;
            """;

        using var conn = conns.Create();
        var row = await conn.QuerySingleOrDefaultAsync<SagaDetailRow>(
            new CommandDefinition(Sql, new { correlationId }, cancellationToken: ct));

        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static int ClampTake(int? take) =>
        take is null or <= 0 ? DefaultTake
        : take > MaxTake     ? MaxTake
        : take.Value;

    private sealed record SagaListRow(
        Guid     CorrelationId,
        string   CurrentState,
        string   TransactionType,
        Guid?    DebitAccountId,
        Guid     CreditAccountId,
        decimal  Amount,
        string   Asset,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record SagaDetailRow(
        Guid     CorrelationId,
        string   CurrentState,
        string   TransactionType,
        Guid?    DebitAccountId,
        Guid     CreditAccountId,
        decimal  Amount,
        string   Asset,
        string?  FailureReason,
        int      VoidAttempts,
        Guid?    HoldExpiryTokenId,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
