using MediatR;
using PlatformWallet.TransactionIntake.Application.Commands.RequestVoid;

namespace PlatformWallet.TransactionIntake.Api.Endpoints;

internal static class VoidEndpoint
{
    private const string VoidRoute   = "/v1/transfer/{correlationId:guid}/void";
    private const string WritePolicy = "ledger:write";

    public static IEndpointRouteBuilder MapVoidEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(VoidRoute, HandleAsync)
            .RequireAuthorization(WritePolicy);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        Guid              correlationId,
        ISender           sender,
        CancellationToken ct)
    {
        await sender.Send(new RequestVoidCommand(correlationId), ct);
        return Results.Accepted();
    }
}
