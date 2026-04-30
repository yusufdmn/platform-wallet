// Minimal webhook receiver used during E2E tests and manual demos.
// Stores the last 1000 deliveries in memory; exposes GET /deliveries to inspect them.
// Not production code — no auth, no persistence.

using System.Collections.Concurrent;
using System.Text.Json;

var deliveries = new ConcurrentQueue<WebhookDelivery>();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:9999");
var app = builder.Build();

app.MapPost("/webhook", async (HttpRequest req) =>
{
    using var ms = new MemoryStream();
    await req.Body.CopyToAsync(ms);
    var body = System.Text.Encoding.UTF8.GetString(ms.ToArray());

    var delivery = new WebhookDelivery(
        ReceivedAt:   DateTimeOffset.UtcNow,
        EventType:    req.Headers["X-Event-Type"].FirstOrDefault() ?? "unknown",
        Signature:    req.Headers["X-Signature"].FirstOrDefault() ?? "",
        CorrelationId: req.Headers["X-Correlation-Id"].FirstOrDefault() ?? "",
        Body:         body
    );

    deliveries.Enqueue(delivery);
    while (deliveries.Count > 1000)
    {
        deliveries.TryDequeue(out _);
    }

    return Results.Ok();
});

app.MapGet("/deliveries", () => Results.Json(deliveries.ToArray()));

app.MapDelete("/deliveries", () =>
{
    while (deliveries.TryDequeue(out _)) { }
    return Results.NoContent();
});

app.MapGet("/healthz", () => Results.Ok("healthy"));

app.Run();

sealed record WebhookDelivery(
    DateTimeOffset ReceivedAt,
    string EventType,
    string Signature,
    string CorrelationId,
    string Body);
