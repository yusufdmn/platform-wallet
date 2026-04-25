using MediatR;
using Microsoft.AspNetCore.Mvc;
using PlatformWallet.TransactionIntake.Application.Commands.SubmitTransfer;

namespace PlatformWallet.TransactionIntake.Api.Endpoints;

internal static class TransferEndpoint
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string TransferRoute        = "/v1/transfer";
    private const string WritePolicy          = "ledger:write";

    public static IEndpointRouteBuilder MapTransferEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(TransferRoute, HandleAsync)
            .RequireAuthorization(WritePolicy);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody]   TransferRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string idempotencyKey,
        ISender      sender,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.BadRequest("Idempotency-Key header is required.");
        }

        var command = new SubmitTransferCommand(
            request.DebitAccountId,
            request.CreditAccountId,
            request.Amount,
            request.Asset,
            idempotencyKey);

        var result = await sender.Send(command, ct);

        return Results.Accepted(value: new { transactionId = result.TransactionId });
    }
}

internal sealed record TransferRequest(
    Guid    DebitAccountId,
    Guid    CreditAccountId,
    decimal Amount,
    string  Asset);
