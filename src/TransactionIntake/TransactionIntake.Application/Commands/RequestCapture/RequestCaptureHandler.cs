using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Exceptions;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Commands.RequestCapture;

public sealed class RequestCaptureHandler(
    ITransactionRepository         transactionRepo,
    IPublishEndpoint               publishEndpoint,
    ILogger<RequestCaptureHandler> logger)
    : IRequestHandler<RequestCaptureCommand>
{
    public async Task Handle(RequestCaptureCommand request, CancellationToken cancellationToken)
    {
        var tx = await transactionRepo.FindByIdAsync(request.CorrelationId, cancellationToken)
            ?? throw new TransactionNotFoundException(request.CorrelationId);

        tx.Transition(TransactionStatus.CaptureRequested);

        await publishEndpoint.Publish(
            new CaptureTransferRequested(request.CorrelationId),
            cancellationToken);

        await transactionRepo.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Capture requested for tx {CorrelationId}", request.CorrelationId);
    }
}
