using MediatR;
using Microsoft.AspNetCore.Mvc;
using PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;

namespace PlatformWallet.TransactionIntake.Api.Endpoints;

internal static class MintEndpoint
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string MintRoute            = "/v1/mint";
    private const string WritePolicy          = "ledger:write";

    public static IEndpointRouteBuilder MapMintEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(MintRoute, HandleAsync)
            .RequireAuthorization(WritePolicy);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody]   MintRequest      request,
        [FromHeader(Name = IdempotencyKeyHeader)] string idempotencyKey,
        ISender      sender,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.BadRequest("Idempotency-Key header is required.");
        }

        var command = new SubmitMintCommand(
            request.CreditAccountId,
            request.Amount,
            request.Asset,
            idempotencyKey);

        var result = await sender.Send(command, ct);

        return Results.Accepted(value: new { transactionId = result.TransactionId });
    }
}

internal sealed record MintRequest(Guid CreditAccountId, decimal Amount, string Asset);
