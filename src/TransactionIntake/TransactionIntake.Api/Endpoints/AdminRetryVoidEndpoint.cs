using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;

namespace PlatformWallet.TransactionIntake.Api.Endpoints;

internal static class AdminRetryVoidEndpoint
{
    private const string Route       = "/admin/transactions/{correlationId:guid}/retry-void";
    private const string AdminPolicy = "ledger:admin";

    public static IEndpointRouteBuilder MapAdminRetryVoidEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost(Route, HandleAsync)
            .RequireAuthorization(AdminPolicy);

        return app;
    }

    // Operator action: re-publishes VoidRequested so a saga parked in VoidStranded
    // re-runs the void. If the saga is in any other state, MassTransit drops the event.
    private static async Task<IResult> HandleAsync(
        Guid                            correlationId,
        IPublishEndpoint                publishEndpoint,
        ILogger<AdminRetryVoidMarker>   logger,
        CancellationToken               ct)
    {
        await publishEndpoint.Publish(new VoidRequested(correlationId), ct);

        logger.LogWarning(
            "Admin retry-void requested for tx {CorrelationId}", correlationId);

        return Results.Accepted();
    }

    private sealed class AdminRetryVoidMarker;
}
