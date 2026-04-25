using MediatR;
using PlatformWallet.TransactionIntake.Application.Commands.RequestCapture;

namespace PlatformWallet.TransactionIntake.Api.Endpoints;

internal static class CaptureEndpoint
{
    private const string CaptureRoute = "/v1/transfer/{correlationId:guid}/capture";
    private const string WritePolicy  = "ledger:write";

    public static IEndpointRouteBuilder MapCaptureEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(CaptureRoute, HandleAsync)
            .RequireAuthorization(WritePolicy);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        Guid              correlationId,
        ISender           sender,
        CancellationToken ct)
    {
        await sender.Send(new RequestCaptureCommand(correlationId), ct);
        return Results.Accepted();
    }
}
