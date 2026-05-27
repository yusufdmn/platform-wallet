using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts.Events;
using ZiggyCreatures.Caching.Fusion;

namespace PlatformWallet.BalanceQuery.Application.Consumers;

public sealed class TransactionVoidedConsumer(
    IFusionCache                        cache,
    ILogger<TransactionVoidedConsumer>  logger) : IConsumer<TransactionVoided>
{
    public async Task Consume(ConsumeContext<TransactionVoided> context)
    {
        var msg = context.Message;
        await cache.RemoveAsync($"balance:{msg.DebitAccountId}",  token: context.CancellationToken);
        await cache.RemoveAsync($"balance:{msg.CreditAccountId}", token: context.CancellationToken);
        logger.LogDebug("Cache invalidated for accounts {Debit} {Credit} on TransactionVoided", msg.DebitAccountId, msg.CreditAccountId);
    }
}
