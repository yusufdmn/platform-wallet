using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

// MintFunds faulted in the Ledger after retries + scheduled redeliveries were
// exhausted (a system fault — e.g. the ledger DB was unavailable). Mint no longer
// flows through the saga, so Intake owns the terminal outcome: publish
// TransactionFailed so the transaction is marked Failed (via TransactionFailedConsumer)
// and the host receives a transaction.failed webhook — matching the prior saga behavior.
public sealed class LedgerFaultedOnMintConsumer(
    ILogger<LedgerFaultedOnMintConsumer> logger) : IConsumer<Fault<MintFunds>>
{
    public async Task Consume(ConsumeContext<Fault<MintFunds>> context)
    {
        var correlationId = context.Message.Message.CorrelationId;
        var reason        = FaultedTransactionHandler.FirstExceptionMessage(context.Message);

        await context.Publish(new TransactionFailed(correlationId, reason));

        logger.LogError(
            "MintFunds system fault for tx {CorrelationId}: {Reason}", correlationId, reason);
    }
}
