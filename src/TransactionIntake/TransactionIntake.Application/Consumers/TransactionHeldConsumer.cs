using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

public sealed class TransactionHeldConsumer(
    ITransactionRepository              repo,
    ILogger<TransactionHeldConsumer>    logger) : IConsumer<TransactionHeld>
{
    public async Task Consume(ConsumeContext<TransactionHeld> context)
    {
        var tx = await repo.FindByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (tx is null) { return; }

        tx.Transition(TransactionStatus.Held);
        await repo.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Transaction {Id} transitioned to Held", tx.Id);
    }
}
