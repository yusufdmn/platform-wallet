using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;

namespace PlatformWallet.TransactionIntake.Application.Commands.RequestVoid;

public sealed class RequestVoidHandler(
    IPublishEndpoint           publishEndpoint,
    ILogger<RequestVoidHandler> logger)
    : IRequestHandler<RequestVoidCommand>
{
    public async Task Handle(RequestVoidCommand request, CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish(
            new VoidRequested(request.CorrelationId),
            cancellationToken);

        logger.LogInformation(
            "Void requested for tx {CorrelationId}", request.CorrelationId);
    }
}
