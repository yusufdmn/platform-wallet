using MassTransit;
using PlatformWallet.Contracts.Events;
using PlatformWallet.WebhookDispatcher.Application.Services;

namespace PlatformWallet.WebhookDispatcher.Application.Consumers;

public sealed class TransactionMintedConsumer(IWebhookDeliveryService delivery) : IConsumer<TransactionMinted>
{
    public Task Consume(ConsumeContext<TransactionMinted> context) =>
        delivery.DeliverAsync(WebhookEventTypes.TransactionMinted, context.Message.CorrelationId, context.CancellationToken);
}
