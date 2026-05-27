using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Exceptions;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Commands.RequestVoid;

public sealed class RequestVoidHandler(
    ITransactionRepository      transactionRepo,
    IPublishEndpoint            publishEndpoint,
    ILogger<RequestVoidHandler> logger)
    : IRequestHandler<RequestVoidCommand>
{
    public async Task Handle(RequestVoidCommand request, CancellationToken cancellationToken)
    {
        var tx = await transactionRepo.FindByIdAsync(request.CorrelationId, cancellationToken)
            ?? throw new TransactionNotFoundException(request.CorrelationId);

        tx.Transition(TransactionStatus.VoidRequested);

        await publishEndpoint.Publish(
            new VoidRequested(request.CorrelationId),
            cancellationToken);

        await transactionRepo.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Void requested for tx {CorrelationId}", request.CorrelationId);
    }
}
