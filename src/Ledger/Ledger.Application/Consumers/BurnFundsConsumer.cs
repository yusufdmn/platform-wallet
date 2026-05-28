using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Ledger.Application.Persistence;
using PlatformWallet.Ledger.Domain;
using PlatformWallet.Ledger.Domain.Exceptions;

namespace PlatformWallet.Ledger.Application.Consumers;

public sealed class BurnFundsConsumer(
    ILedgerRepository repository,
    ILogger<BurnFundsConsumer> logger) : IConsumer<BurnFunds>
{
    public async Task Consume(ConsumeContext<BurnFunds> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        try
        {
            var world = await repository.GetAccountAsync(SystemAccounts.WorldId, ct)
                ?? throw new SystemAccountNotFoundException("@world");

            var debitAccount = await repository.GetAccountAsync(msg.DebitAccountId, ct)
                ?? throw new AccountNotFoundException(msg.DebitAccountId);

            ValidateAsset(world,        msg.Asset);
            ValidateAsset(debitAccount, msg.Asset);

            var (debit, credit) = PostingPairBuilder.BuildBurn(
                msg.CorrelationId, msg.DebitAccountId, msg.Amount, msg.Asset);

            debitAccount.ApplyDebit(msg.Amount);
            world.ApplyCredit(msg.Amount);

            repository.AddPosting(debit);
            repository.AddPosting(credit);

            await context.Publish(new FundsBurned(msg.CorrelationId), ct);

            await repository.SaveChangesAsync(ct);

            logger.LogInformation(
                "Burned {Amount} {Asset} from account {AccountId} for tx {CorrelationId}",
                msg.Amount, msg.Asset, msg.DebitAccountId, msg.CorrelationId);
        }
        catch (LedgerDomainException ex)
        {
            await context.Publish(new BurnFailed(msg.CorrelationId, ex.Message), ct);

            logger.LogWarning(
                "Burn failed for tx {CorrelationId}: {Reason}", msg.CorrelationId, ex.Message);
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
