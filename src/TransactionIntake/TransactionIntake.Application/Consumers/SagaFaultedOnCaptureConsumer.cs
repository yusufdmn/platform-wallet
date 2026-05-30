using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Persistence;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

public sealed class SagaFaultedOnCaptureConsumer(
    ITransactionRepository                    repo,
    ILogger<SagaFaultedOnCaptureConsumer>     logger) : IConsumer<Fault<CaptureTransferRequested>>
{
    public Task Consume(ConsumeContext<Fault<CaptureTransferRequested>> context) =>
        FaultedTransactionHandler.MarkFailedAsync(
            repo,
            logger,
            context.Message.Message.CorrelationId,
            FaultedTransactionHandler.FirstExceptionMessage(context.Message),
            "capture",
            context.CancellationToken);
}
