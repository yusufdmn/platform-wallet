using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using ZiggyCreatures.Caching.Fusion;

namespace PlatformWallet.BalanceQuery.Application.Consumers;

public sealed class TransactionMintedConsumer(
    IFusionCache                        cache,
    ILogger<TransactionMintedConsumer>  logger) : IConsumer<TransactionMinted>
{
    public async Task Consume(ConsumeContext<TransactionMinted> context)
    {
        var msg = context.Message;
        if (msg.DebitAccountId.HasValue)
        {
            await cache.RemoveAsync($"balance:{msg.DebitAccountId.Value}", token: context.CancellationToken);
        }
        await cache.RemoveAsync($"balance:{msg.CreditAccountId}", token: context.CancellationToken);
        logger.LogDebug("Cache invalidated for account {Credit} on TransactionMinted", msg.CreditAccountId);
    }
}
