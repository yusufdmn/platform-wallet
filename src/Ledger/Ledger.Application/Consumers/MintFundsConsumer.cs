using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Commands;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Ledger.Application.Persistence;
using PlatformWallet.Ledger.Domain;

namespace PlatformWallet.Ledger.Application.Consumers;

public sealed class MintFundsConsumer(
    ILedgerRepository repository,
    ILogger<MintFundsConsumer> logger) : IConsumer<MintFunds>
{
    public async Task Consume(ConsumeContext<MintFunds> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        var world = await repository.GetAccountAsync(SystemAccounts.WorldId, ct)
            ?? throw new InvalidOperationException("@world system account not found — run migrations.");

        var creditAccount = await repository.GetAccountAsync(msg.CreditAccountId, ct);
        if (creditAccount is null)
        {
            creditAccount = Account.Create(msg.CreditAccountId, name: null, asset: msg.Asset);
            repository.AddAccount(creditAccount);
        }

        ValidateAsset(world, msg.Asset);
        ValidateAsset(creditAccount, msg.Asset);

        var (debit, credit) = PostingPairBuilder.BuildMint(msg.CorrelationId, msg.CreditAccountId, msg.Amount, msg.Asset);

        world.ApplyDebit(msg.Amount);
        creditAccount.ApplyCredit(msg.Amount);

        repository.AddPosting(debit);
        repository.AddPosting(credit);

        await repository.SaveChangesAsync(ct);

        await context.Publish(new FundsMinted(msg.CorrelationId), ct);

        logger.LogInformation(
            "Minted {Amount} {Asset} to account {AccountId} for tx {CorrelationId}",
            msg.Amount, msg.Asset, msg.CreditAccountId, msg.CorrelationId);
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
