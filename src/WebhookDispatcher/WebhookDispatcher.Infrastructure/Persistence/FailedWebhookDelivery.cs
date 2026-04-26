namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

public sealed class FailedWebhookDelivery
{
    private const int MaxReasonLength       = 2000;
    private const int MaxResponseBodyLength = 8192;

    public long    Id                   { get; private set; }
    public string  EventType            { get; private set; } = null!;
    public Guid    CorrelationId        { get; private set; }
    public string  Reason               { get; private set; } = null!;
    public int?    LastHttpStatusCode   { get; private set; }
    public string? LastHttpResponseBody { get; private set; }
    public DateTimeOffset FailedAt      { get; private set; }

    private FailedWebhookDelivery() { }

    public static FailedWebhookDelivery Create(
        string eventType,
        Guid correlationId,
        string reason,
        int? lastHttpStatusCode      = null,
        string? lastHttpResponseBody = null) =>
        new()
        {
            EventType            = eventType,
            CorrelationId        = correlationId,
            Reason               = Truncate(reason, MaxReasonLength)!,
            LastHttpStatusCode   = lastHttpStatusCode,
            LastHttpResponseBody = Truncate(lastHttpResponseBody, MaxResponseBodyLength),
            FailedAt             = DateTimeOffset.UtcNow,
        };

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
