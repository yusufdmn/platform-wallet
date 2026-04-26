using MassTransit;
using PlatformWallet.Contracts.Events;
using PlatformWallet.WebhookDispatcher.Application.Services;

namespace PlatformWallet.WebhookDispatcher.Application.Consumers;

public sealed class TransactionCapturedConsumer(IWebhookDeliveryService delivery) : IConsumer<TransactionCaptured>
{
    public Task Consume(ConsumeContext<TransactionCaptured> context) =>
        delivery.DeliverAsync(WebhookEventTypes.TransactionCaptured, context.Message.CorrelationId, context.CancellationToken);
}
