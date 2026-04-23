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

        var creditAccount = await repository.GetAccountAsync(msg.CreditAccountId, ct)
            ?? throw new InvalidOperationException($"Account {msg.CreditAccountId} not found.");

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
}
