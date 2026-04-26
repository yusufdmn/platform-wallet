namespace PlatformWallet.WebhookDispatcher.Application.Services;

public interface IFailedDeliveryRepository
{
    Task PersistAsync(
        string eventType,
        Guid correlationId,
        string reason,
        int? lastHttpStatusCode,
        string? lastHttpResponseBody,
        CancellationToken ct);
}
