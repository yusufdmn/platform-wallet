using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Ledger.Application.Persistence;
using PlatformWallet.Ledger.Domain;
using PlatformWallet.Ledger.Domain.Exceptions;

namespace PlatformWallet.Ledger.Application.Consumers;

public sealed class CaptureTransferConsumer(
    ILedgerRepository repository,
    ILogger<CaptureTransferConsumer> logger) : IConsumer<CaptureTransfer>
{
    public async Task Consume(ConsumeContext<CaptureTransfer> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        try
        {
            var heldPool      = await repository.GetAccountAsync(SystemAccounts.HeldPoolId, ct)
                ?? throw new SystemAccountNotFoundException("@held_pool");

            var creditAccount = await repository.GetAccountAsync(msg.CreditAccountId, ct);
            if (creditAccount is null)
            {
                creditAccount = Account.Create(msg.CreditAccountId, name: null, asset: msg.Asset);
                repository.AddAccount(creditAccount);
            }

            var debitAccount  = await repository.GetAccountAsync(msg.DebitAccountId, ct)
                ?? throw new AccountNotFoundException(msg.DebitAccountId);

            ValidateAsset(heldPool,      msg.Asset);
            ValidateAsset(creditAccount, msg.Asset);
            ValidateAsset(debitAccount,  msg.Asset);

            var (debit, credit) = PostingPairBuilder.BuildCapture(
                msg.CorrelationId, SystemAccounts.HeldPoolId, msg.CreditAccountId, msg.Amount, msg.Asset);

            debitAccount.CaptureHeld(msg.Amount);
            heldPool.ApplyDebit(msg.Amount);
            creditAccount.ApplyCredit(msg.Amount);

            repository.AddPosting(debit);
            repository.AddPosting(credit);

            await context.Publish(new TransferCaptured(msg.CorrelationId), ct);

            await repository.SaveChangesAsync(ct);

            logger.LogInformation(
                "Captured {Amount} {Asset} from {DebitAccountId} to {CreditAccountId} for tx {CorrelationId}",
                msg.Amount, msg.Asset, msg.DebitAccountId, msg.CreditAccountId, msg.CorrelationId);
        }
        catch (LedgerDomainException ex)
        {
            await context.Publish(new CaptureFailed(msg.CorrelationId, ex.Message), ct);

            logger.LogWarning(
                "Capture failed for tx {CorrelationId}: {Reason}", msg.CorrelationId, ex.Message);
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
