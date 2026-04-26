using MassTransit;
using Microsoft.Extensions.Logging;
using PlatformWallet.Contracts;
using PlatformWallet.WebhookDispatcher.Application.Services;

namespace PlatformWallet.WebhookDispatcher.Application.Consumers;

public sealed class WebhookFaultConsumer<TEvent>(
    IFailedDeliveryRepository repository,
    ILogger<WebhookFaultConsumer<TEvent>> logger)
    : IConsumer<Fault<TEvent>>
    where TEvent : class, ITransactionMessage
{
    private const int MaxReasonLength = 2000;

    public async Task Consume(ConsumeContext<Fault<TEvent>> context)
    {
        var fault         = context.Message;
        var eventType     = WebhookEventTypes.Resolve<TEvent>();
        var correlationId = fault.Message.CorrelationId;
        var exceptionInfo = fault.Exceptions.FirstOrDefault();
        var rawReason     = exceptionInfo?.Message ?? "Unknown";
        var reason        = rawReason.Length > MaxReasonLength
            ? rawReason[..MaxReasonLength]
            : rawReason;

        int? statusCode   = null;
        string? respBody  = null;

        if (exceptionInfo?.ExceptionType == typeof(WebhookDeliveryException).FullName)
        {
            ExtractHttpDetails(rawReason, out statusCode, out respBody);
        }

        logger.LogError(
            "Webhook delivery permanently failed for {EventType} / {CorrelationId}: {Reason}",
            eventType, correlationId, reason);

        await repository.PersistAsync(
            eventType, correlationId, reason, statusCode, respBody, context.CancellationToken);
    }

    private static void ExtractHttpDetails(string message, out int? statusCode, out string? responseBody)
    {
        statusCode   = null;
        responseBody = null;

        // Message format: "HTTP {statusCode}: {responseBody}"
        if (!message.StartsWith("HTTP ", StringComparison.Ordinal))
        {
            return;
        }

        var colonIndex = message.IndexOf(':', 5);
        if (colonIndex < 0)
        {
            return;
        }

        if (int.TryParse(message.AsSpan(5, colonIndex - 5), out var code))
        {
            statusCode   = code;
            responseBody = message[(colonIndex + 2)..];
        }
    }
}
