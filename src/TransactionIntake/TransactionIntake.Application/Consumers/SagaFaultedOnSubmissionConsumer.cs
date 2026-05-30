using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

// Saga consumer for TransactionSubmitted faulted (exhausted retries + scheduled
// redeliveries). Marks the Intake transaction Failed so it no longer appears Pending.
public sealed class SagaFaultedOnSubmissionConsumer(
    ITransactionRepository                       repo,
    ILogger<SagaFaultedOnSubmissionConsumer>     logger) : IConsumer<Fault<TransactionSubmitted>>
{
    public Task Consume(ConsumeContext<Fault<TransactionSubmitted>> context) =>
        FaultedTransactionHandler.MarkFailedAsync(
            repo,
            logger,
            context.Message.Message.CorrelationId,
            FaultedTransactionHandler.FirstExceptionMessage(context.Message),
            "submission",
            context.CancellationToken);
}
