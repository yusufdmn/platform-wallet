using MassTransit;
using PlatformWallet.Contracts.Events;
using PlatformWallet.WebhookDispatcher.Application.Services;

namespace PlatformWallet.WebhookDispatcher.Application.Consumers;

public sealed class TransactionVoidedConsumer(IWebhookDeliveryService delivery) : IConsumer<TransactionVoided>
{
    public Task Consume(ConsumeContext<TransactionVoided> context) =>
        delivery.DeliverAsync(WebhookEventTypes.TransactionVoided, context.Message.CorrelationId, context.CancellationToken);
}
