using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

public sealed class TransactionFailedConsumer(
    ITransactionRepository            repo,
    ILogger<TransactionFailedConsumer> logger) : IConsumer<TransactionFailed>
{
    public async Task Consume(ConsumeContext<TransactionFailed> context)
    {
        var tx = await repo.FindByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (tx is null) { return; }

        tx.Transition(TransactionStatus.Failed);
        await repo.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Transaction {Id} transitioned to Failed: {Reason}", tx.Id, context.Message.Reason);
    }
}
