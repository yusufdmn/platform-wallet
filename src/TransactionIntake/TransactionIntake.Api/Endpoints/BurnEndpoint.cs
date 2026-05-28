using MediatR;
using Microsoft.AspNetCore.Mvc;
using PlatformWallet.TransactionIntake.Application.Commands.SubmitBurn;

namespace PlatformWallet.TransactionIntake.Api.Endpoints;

internal static class BurnEndpoint
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string BurnRoute            = "/burn";
    private const string WritePolicy          = "ledger:write";

    public static IEndpointRouteBuilder MapBurnEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(BurnRoute, HandleAsync)
            .RequireAuthorization(WritePolicy);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody]   BurnRequest      request,
        [FromHeader(Name = IdempotencyKeyHeader)] string idempotencyKey,
        ISender      sender,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.BadRequest("Idempotency-Key header is required.");
        }

        var command = new SubmitBurnCommand(
            request.DebitAccountId,
            request.Amount,
            request.Asset,
            idempotencyKey);

        var result = await sender.Send(command, ct);

        return Results.Accepted(value: new { transactionId = result.TransactionId });
    }
}

internal sealed record BurnRequest(Guid DebitAccountId, decimal Amount, string Asset);
