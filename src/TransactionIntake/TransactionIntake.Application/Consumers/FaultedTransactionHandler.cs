using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Domain;

namespace PlatformWallet.TransactionIntake.Application.Consumers;

// Shared by SagaFaultedOn{Submission,Capture,Void}Consumer: resolves the
// Intake row by correlation id, transitions it to Failed, and persists.
internal static class FaultedTransactionHandler
{
    private const string UnknownFault = "Unknown fault";

    public static async Task MarkFailedAsync(
        ITransactionRepository repo,
        ILogger                logger,
        Guid                   correlationId,
        string                 reason,
        string                 stage,
        CancellationToken      ct)
    {
        var tx = await repo.FindByIdAsync(correlationId, ct);
        if (tx is null)
        {
            logger.LogWarning(
                "Saga fault on {Stage} for unknown transaction {CorrelationId}",
                stage, correlationId);
            return;
        }

        tx.Transition(TransactionStatus.Failed);
        await repo.SaveChangesAsync(ct);

        logger.LogError(
            "Transaction {CorrelationId} marked Failed after saga {Stage} fault: {Reason}",
            correlationId, stage, reason);
    }

    public static string FirstExceptionMessage<T>(Fault<T> fault) where T : class =>
        fault.Exceptions.FirstOrDefault()?.Message ?? UnknownFault;
}
