namespace PlatformWallet.WebhookDispatcher.Application.Services;

public interface IFailedDeliveryRetrier
{
    Task<RetryOutcome?> RetryAsync(long id, CancellationToken ct);
}

public sealed record RetryOutcome(long Id, string Status, int RetryCount, bool Delivered);
