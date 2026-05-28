using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

public sealed class TransactionBurnedConsumer(
    ITransactionRepository             repo,
    ILogger<TransactionBurnedConsumer> logger) : IConsumer<TransactionBurned>
{
    public async Task Consume(ConsumeContext<TransactionBurned> context)
    {
        var tx = await repo.FindByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (tx is null) { return; }

        tx.Transition(TransactionStatus.Captured);
        await repo.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Transaction {Id} transitioned to Captured (burned)", tx.Id);
    }
}
