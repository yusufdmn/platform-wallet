using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using ZiggyCreatures.Caching.Fusion;

namespace PlatformWallet.BalanceQuery.Application.Consumers;

public sealed class TransactionBurnedConsumer(
    IFusionCache                        cache,
    ILogger<TransactionBurnedConsumer>  logger) : IConsumer<TransactionBurned>
{
    public async Task Consume(ConsumeContext<TransactionBurned> context)
    {
        var msg = context.Message;
        await cache.RemoveAsync($"balance:{msg.DebitAccountId}",  token: context.CancellationToken);
        await cache.RemoveAsync($"balance:{msg.CreditAccountId}", token: context.CancellationToken);
        logger.LogDebug("Cache invalidated for accounts {Debit} {Credit} on TransactionBurned", msg.DebitAccountId, msg.CreditAccountId);
    }
}
