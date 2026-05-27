using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using ZiggyCreatures.Caching.Fusion;

namespace PlatformWallet.BalanceQuery.Application.Consumers;

public sealed class TransactionCapturedConsumer(
    IFusionCache                            cache,
    ILogger<TransactionCapturedConsumer>    logger) : IConsumer<TransactionCaptured>
{
    public async Task Consume(ConsumeContext<TransactionCaptured> context)
    {
        var msg = context.Message;
        await cache.RemoveAsync($"balance:{msg.DebitAccountId}",  token: context.CancellationToken);
        await cache.RemoveAsync($"balance:{msg.CreditAccountId}", token: context.CancellationToken);
        logger.LogDebug("Cache invalidated for accounts {Debit} {Credit} on TransactionCaptured", msg.DebitAccountId, msg.CreditAccountId);
    }
}
