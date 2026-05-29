using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Ledger.Application.Persistence;
using PlatformWallet.Ledger.Domain;
using PlatformWallet.Ledger.Domain.Exceptions;

namespace PlatformWallet.Ledger.Application.Consumers;

public sealed class HoldFundsConsumer(
    ILedgerRepository repository,
    ILogger<HoldFundsConsumer> logger) : IConsumer<HoldFunds>
{
    public async Task Consume(ConsumeContext<HoldFunds> context)
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

            var (debit, credit) = PostingPairBuilder.BuildHold(
                msg.CorrelationId, msg.DebitAccountId, msg.Amount, msg.Asset);

            debitAccount.ReserveHold(msg.Amount);
            heldPool.ApplyCredit(msg.Amount);

            repository.AddPosting(debit);
            repository.AddPosting(credit);

            await context.Publish(new FundsHeld(msg.CorrelationId), ct);

            await repository.SaveChangesAsync(ct);

            logger.LogInformation(
                "Held {Amount} {Asset} on account {AccountId} for tx {CorrelationId}",
                msg.Amount, msg.Asset, msg.DebitAccountId, msg.CorrelationId);
        }
        catch (LedgerDomainException ex)
        {
            await context.Publish(new HoldFailed(msg.CorrelationId, ex.Message), ct);

            logger.LogWarning(
                "Hold failed for tx {CorrelationId}: {Reason}", msg.CorrelationId, ex.Message);
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
