using Microsoft.AspNetCore.Mvc;
using PlatformWallet.TransactionIntake.Application.Persistence;

namespace PlatformWallet.TransactionIntake.Api.Endpoints;

internal static class GetTransactionEndpoint
{
    private const string Route      = "/transactions/{id:guid}";
    private const string ReadPolicy = "ledger:read";

    public static IEndpointRouteBuilder MapGetTransactionEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(Route, HandleAsync)
            .RequireAuthorization(ReadPolicy);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        [FromRoute] Guid id,
        ITransactionRepository repo,
        CancellationToken ct)
    {
        var tx = await repo.FindByIdAsync(id, ct);
        if (tx is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new
        {
            transactionId = tx.Id,
            status        = tx.Status.ToString(),
            asset         = tx.Asset,
            amount        = tx.Amount,
            createdAt     = tx.CreatedAt,
        });
    }
}
