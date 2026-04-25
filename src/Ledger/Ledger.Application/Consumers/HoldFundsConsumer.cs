using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Ledger.Application.Persistence;
using PlatformWallet.Ledger.Domain;

namespace PlatformWallet.Ledger.Application.Consumers;

public sealed class HoldFundsConsumer(
    ILedgerRepository repository,
    ILogger<HoldFundsConsumer> logger) : IConsumer<HoldFunds>
{
    public async Task Consume(ConsumeContext<HoldFunds> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        var heldPool     = await repository.GetAccountAsync(SystemAccounts.HeldPoolId, ct)
            ?? throw new InvalidOperationException("@held_pool system account not found — run migrations.");

        var debitAccount = await repository.GetAccountAsync(msg.DebitAccountId, ct)
            ?? throw new InvalidOperationException($"Account {msg.DebitAccountId} not found.");

        ValidateAsset(heldPool,     msg.Asset);
        ValidateAsset(debitAccount, msg.Asset);

        var (debit, credit) = PostingPairBuilder.BuildHold(
            msg.CorrelationId, msg.DebitAccountId, msg.Amount, msg.Asset);

        debitAccount.ReserveHold(msg.Amount);
        heldPool.ApplyCredit(msg.Amount);

        repository.AddPosting(debit);
        repository.AddPosting(credit);

        await repository.SaveChangesAsync(ct);

        await context.Publish(new FundsHeld(msg.CorrelationId), ct);

        logger.LogInformation(
            "Held {Amount} {Asset} on account {AccountId} for tx {CorrelationId}",
            msg.Amount, msg.Asset, msg.DebitAccountId, msg.CorrelationId);
    }

    private static void ValidateAsset(Account account, string expectedAsset)
    {
        if (!string.Equals(account.Asset, expectedAsset, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Asset mismatch on account {account.Id}: account asset='{account.Asset}', message asset='{expectedAsset}'.");
        }
    }
}
