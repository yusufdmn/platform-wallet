using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlatformWallet.WebhookDispatcher.Application.Services;
using PlatformWallet.WebhookDispatcher.Domain;
using PlatformWallet.WebhookDispatcher.Infrastructure.Configuration;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Services;

internal sealed class WebhookDeliveryService(
    IHttpClientFactory httpClientFactory,
    IOptions<WebhookOptions> options,
    ILogger<WebhookDeliveryService> logger) : IWebhookDeliveryService
{
    private const string SignatureHeader     = "X-Signature";
    private const string EventTypeHeader     = "X-Event-Type";
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private const string HttpClientName      = "webhook";

    public async Task DeliverAsync(string eventType, Guid correlationId, CancellationToken ct)
    {
        // Payload has no timestamp: same (eventType, correlationId) always produces
        // identical bytes, so the HMAC signature is stable across every retry and
        // MassTransit redelivery — receivers can verify it on any attempt.
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            event_type     = eventType,
            correlation_id = correlationId,
        });

        var secretBytes = Encoding.UTF8.GetBytes(options.Value.HmacSecret);
        var signature   = HmacSigner.Sign(payload, secretBytes);

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.TargetUrl)
        {
            Content = new ByteArrayContent(payload),
        };

        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add(SignatureHeader,     signature);
        request.Headers.Add(EventTypeHeader,     eventType);
        request.Headers.Add(CorrelationIdHeader, correlationId.ToString());

        logger.LogDebug(
            "Delivering webhook {EventType} for correlation {CorrelationId} to {TargetUrl}",
            eventType, correlationId, options.Value.TargetUrl);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new WebhookDeliveryException((int)response.StatusCode, body);
        }
    }
}
