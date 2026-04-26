namespace PlatformWallet.WebhookDispatcher.Application.Services;

public interface IWebhookDeliveryService
{
    Task DeliverAsync(string eventType, Guid correlationId, CancellationToken ct);
}
