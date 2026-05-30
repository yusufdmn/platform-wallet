using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Persistence;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

public sealed class SagaFaultedOnVoidConsumer(
    ITransactionRepository                 repo,
    ILogger<SagaFaultedOnVoidConsumer>     logger) : IConsumer<Fault<VoidRequested>>
{
    public Task Consume(ConsumeContext<Fault<VoidRequested>> context) =>
        FaultedTransactionHandler.MarkFailedAsync(
            repo,
            logger,
            context.Message.Message.CorrelationId,
            FaultedTransactionHandler.FirstExceptionMessage(context.Message),
            "void",
            context.CancellationToken);
}
