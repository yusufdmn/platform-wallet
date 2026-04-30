using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

public sealed class TransactionVoidedConsumer(
    ITransactionRepository             repo,
    ILogger<TransactionVoidedConsumer> logger) : IConsumer<TransactionVoided>
{
    public async Task Consume(ConsumeContext<TransactionVoided> context)
    {
        var tx = await repo.FindByIdAsync(context.Message.CorrelationId, context.CancellationToken);
        if (tx is null) { return; }

        tx.Transition(TransactionStatus.Voided);
        await repo.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Transaction {Id} transitioned to Voided", tx.Id);
    }
}
