using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlatformWallet.ApiGateway.Yarp.Infrastructure.Rabbit;

public interface IRabbitMqManagementClient
{
    Task<IReadOnlyList<DlqQueueInfo>>     ListDlqAsync(CancellationToken ct);
    Task<IReadOnlyList<DlqMessage>>   PeekAsync(string queue, int take, CancellationToken ct);
    Task<DlqMessage?>                 DrainOneAsync(string queue, CancellationToken ct);
    Task                              PublishAsync(string queue, DlqMessage message, CancellationToken ct);
}

public sealed record DlqQueueInfo(string Name, int Messages);

public sealed record DlqMessage(
    [property: JsonPropertyName("payload")]          string Payload,
    [property: JsonPropertyName("payload_encoding")] string PayloadEncoding,
    [property: JsonPropertyName("properties")]       JsonElement? Properties,
    [property: JsonPropertyName("routing_key")]      string? RoutingKey);

public sealed class RabbitMqManagementClient(
    HttpClient                  http,
    RabbitMqManagementOptions   options) : IRabbitMqManagementClient
{
    private const string ErrorQueueSuffix = "_error";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<DlqQueueInfo>> ListDlqAsync(CancellationToken ct)
    {
        var path  = $"api/queues/{EncodeVhost(options.Vhost)}";
        var queues = await http.GetFromJsonAsync<List<QueueListItem>>(path, JsonOpts, ct)
                     ?? new();

        return queues
            .Where(q => q.Name.EndsWith(ErrorQueueSuffix, StringComparison.Ordinal))
            .Select(q => new DlqQueueInfo(q.Name, q.Messages))
            .ToList();
    }

    public async Task<IReadOnlyList<DlqMessage>> PeekAsync(string queue, int take, CancellationToken ct) =>
        await GetMessagesAsync(queue, take, ackMode: "ack_requeue_true", ct);

    public async Task<DlqMessage?> DrainOneAsync(string queue, CancellationToken ct)
    {
        var messages = await GetMessagesAsync(queue, count: 1, ackMode: "ack_requeue_false", ct);
        return messages.Count > 0 ? messages[0] : null;
    }

    public async Task PublishAsync(string queue, DlqMessage message, CancellationToken ct)
    {
        var path = $"api/exchanges/{EncodeVhost(options.Vhost)}/amq.default/publish";
        var body = new
        {
            properties       = message.Properties,
            routing_key      = StripErrorSuffix(queue),
            payload          = message.Payload,
            payload_encoding = message.PayloadEncoding,
        };

        var response = await http.PostAsJsonAsync(path, body, JsonOpts, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task<IReadOnlyList<DlqMessage>> GetMessagesAsync(
        string queue, int count, string ackMode, CancellationToken ct)
    {
        var path = $"api/queues/{EncodeVhost(options.Vhost)}/{Uri.EscapeDataString(queue)}/get";
        var body = new
        {
            count    = count,
            ackmode  = ackMode,
            encoding = "auto",
            truncate = 50000,
        };

        var response = await http.PostAsJsonAsync(path, body, JsonOpts, ct);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return Array.Empty<DlqMessage>();
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<DlqMessage>>(JsonOpts, ct)
               ?? new List<DlqMessage>();
    }

    private static string StripErrorSuffix(string queue) =>
        queue.EndsWith(ErrorQueueSuffix, StringComparison.Ordinal)
            ? queue[..^ErrorQueueSuffix.Length]
            : queue;

    private static string EncodeVhost(string vhost) =>
        Uri.EscapeDataString(vhost);

    private sealed record QueueListItem(
        [property: JsonPropertyName("name")]     string Name,
        [property: JsonPropertyName("messages")] int    Messages);
}
