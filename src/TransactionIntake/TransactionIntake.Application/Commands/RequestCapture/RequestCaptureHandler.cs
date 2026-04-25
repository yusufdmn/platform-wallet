using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;

namespace PlatformWallet.TransactionIntake.Application.Commands.RequestCapture;

public sealed class RequestCaptureHandler(
    IPublishEndpoint           publishEndpoint,
    ILogger<RequestCaptureHandler> logger)
    : IRequestHandler<RequestCaptureCommand>
{
    public async Task Handle(RequestCaptureCommand request, CancellationToken cancellationToken)
    {
        await publishEndpoint.Publish(
            new CaptureTransferRequested(request.CorrelationId),
            cancellationToken);

        logger.LogInformation(
            "Capture requested for tx {CorrelationId}", request.CorrelationId);
    }
}
