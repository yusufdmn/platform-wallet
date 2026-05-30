namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

public enum FailedDeliveryStatus
{
    Failed    = 0,
    Retrying  = 1,
    Delivered = 2,
}
