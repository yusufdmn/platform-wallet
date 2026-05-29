using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Ledger.Application.Persistence;
using PlatformWallet.Ledger.Domain;
using PlatformWallet.Ledger.Domain.Exceptions;

namespace PlatformWallet.Ledger.Application.Consumers;

public sealed class VoidHoldConsumer(
    ILedgerRepository repository,
    ILogger<VoidHoldConsumer> logger) : IConsumer<VoidHold>
{
    public async Task Consume(ConsumeContext<VoidHold> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        try
        {
            var heldPool     = await repository.GetAccountAsync(SystemAccounts.HeldPoolId, ct)
                ?? throw new SystemAccountNotFoundException("@held_pool");

            var debitAccount = await repository.GetAccountAsync(msg.DebitAccountId, ct)
                ?? throw new AccountNotFoundException(msg.DebitAccountId);

            ValidateAsset(heldPool,     msg.Asset);
            ValidateAsset(debitAccount, msg.Asset);

            var (debit, credit) = PostingPairBuilder.BuildVoid(
                msg.CorrelationId, msg.DebitAccountId, msg.Amount, msg.Asset);

            debitAccount.ReleaseHold(msg.Amount);
            heldPool.ApplyDebit(msg.Amount);

            repository.AddPosting(debit);
            repository.AddPosting(credit);

            await context.Publish(new HoldVoided(msg.CorrelationId), ct);

            await repository.SaveChangesAsync(ct);

            logger.LogInformation(
                "Voided hold of {Amount} {Asset} on account {AccountId} for tx {CorrelationId}",
                msg.Amount, msg.Asset, msg.DebitAccountId, msg.CorrelationId);
        }
        catch (LedgerDomainException ex)
        {
            await context.Publish(new VoidFailed(msg.CorrelationId, ex.Message), ct);

            logger.LogWarning(
                "Void failed for tx {CorrelationId}: {Reason}", msg.CorrelationId, ex.Message);
        }
    }

    private static void ValidateAsset(Account account, string expectedAsset)
    {
        if (!string.Equals(account.Asset, expectedAsset, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssetMismatchException(account.Id, account.Asset, expectedAsset);
        }
    }
}
