using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using UberEatsWallet.Application.Webhooks;
using UberEatsWallet.Domain;

namespace UberEatsWallet.Web.Controllers;

/// <summary>
/// Receives HMAC-signed wallet webhooks. Verifies the signature over the raw body, then applies the
/// event to the matching order. The payload is a "go-look" notification: <c>(event_type, correlation_id)</c>.
/// </summary>
[ApiController]
[Route("webhooks/wallet")]
public sealed class WebhooksController(
    WalletWebhookProcessor processor,
    IConfiguration configuration,
    ILogger<WebhooksController> logger) : ControllerBase
{
    private const string SignatureHeader = "X-Signature";
    private const string SecretKey = "WEBHOOK_HMAC_SECRET";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();

        var secret = configuration[SecretKey];
        if (string.IsNullOrEmpty(secret))
        {
            logger.LogError("{Key} is not configured; cannot verify webhooks.", SecretKey);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (!WebhookSignatureVerifier.IsValid(body, secret, Request.Headers[SignatureHeader]))
        {
            return Unauthorized();
        }

        var payload = JsonSerializer.Deserialize<WebhookPayload>(body, JsonOptions);
        if (payload is null || string.IsNullOrEmpty(payload.EventType))
        {
            return BadRequest();
        }

        await processor.HandleAsync(payload.EventType, payload.CorrelationId, ct);
        return Ok();
    }

    private sealed record WebhookPayload(
        [property: JsonPropertyName("event_type")] string EventType,
        [property: JsonPropertyName("correlation_id")] Guid CorrelationId);
}
