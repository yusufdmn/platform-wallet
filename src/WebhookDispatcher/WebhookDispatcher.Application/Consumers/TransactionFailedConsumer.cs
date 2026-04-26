using MassTransit;
using PlatformWallet.Contracts.Events;
using PlatformWallet.WebhookDispatcher.Application.Services;

namespace PlatformWallet.WebhookDispatcher.Application.Consumers;

public sealed class TransactionFailedConsumer(IWebhookDeliveryService delivery) : IConsumer<TransactionFailed>
{
    public Task Consume(ConsumeContext<TransactionFailed> context) =>
        delivery.DeliverAsync(WebhookEventTypes.TransactionFailed, context.Message.CorrelationId, context.CancellationToken);
}
