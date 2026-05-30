using MassTransit;
using PlatformWallet.Contracts.Events;
using PlatformWallet.WebhookDispatcher.Application.Services;

namespace PlatformWallet.WebhookDispatcher.Application.Consumers;

public sealed class TransactionBurnedConsumer(IWebhookDeliveryService delivery) : IConsumer<TransactionBurned>
{
    public Task Consume(ConsumeContext<TransactionBurned> context) =>
        delivery.DeliverAsync(WebhookEventTypes.TransactionBurned, context.Message.CorrelationId, context.CancellationToken);
}
