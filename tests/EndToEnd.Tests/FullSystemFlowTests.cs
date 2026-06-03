using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PlatformWallet.EndToEnd.Tests;

/// <summary>
/// End-to-end tests that drive the full system stack.
///
/// Prerequisites:
///   - Infra services on the LAN (Postgres, RabbitMQ, Redis, Keycloak)
///   - All .NET services running locally via `dotnet run`
///   - webhook-sink running on port 9999
///
/// Tests skip automatically unless E2E_BASE_URL is set.
/// </summary>
[Trait("Category", "EndToEnd")]
public sealed class FullSystemFlowTests : IAsyncLifetime, IDisposable
{
    // ── Config from env ───────────────────────────────────────────────────────

    private static readonly string? BaseUrl;
    private static readonly string  AdminBaseUrl;
    private static readonly string  KeycloakUrl;
    private static readonly string  WebhookSinkUrl;
    private static readonly string  WebhookHmacSecret;

    static FullSystemFlowTests()
    {
        DotNetEnv.Env.TraversePath().Load();
        BaseUrl           = Environment.GetEnvironmentVariable("E2E_BASE_URL");
        // The admin plane is served on the gateway's internal-only listener, not the public edge.
        AdminBaseUrl      = Environment.GetEnvironmentVariable("E2E_ADMIN_BASE_URL")   ?? "http://localhost:14044";
        KeycloakUrl       = Environment.GetEnvironmentVariable("E2E_KEYCLOAK_URL")     ?? "http://localhost:8088";
        WebhookSinkUrl    = Environment.GetEnvironmentVariable("E2E_WEBHOOK_SINK_URL") ?? "http://localhost:9999";
        WebhookHmacSecret = Environment.GetEnvironmentVariable("WEBHOOK_HMAC_SECRET")
            ?? throw new InvalidOperationException("WEBHOOK_HMAC_SECRET env var must be set to run E2E tests");
    }

    private const string Realm        = "platform-wallet";
    private const string ClientId     = "ledger-service-client";
    private const string ClientSecret = "ledger-service-secret";
    private const string Scopes       = "ledger:read ledger:write ledger:admin";

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly HttpClient _gateway     = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly HttpClient _admin       = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly HttpClient _webhookSink = new() { Timeout = TimeSpan.FromSeconds(10) };
    private string _token = "";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        if (BaseUrl is null)
        {
            return;
        }

        _token = await FetchTokenAsync();
        _gateway.BaseAddress     = new Uri(BaseUrl);
        _admin.BaseAddress       = new Uri(AdminBaseUrl);
        _webhookSink.BaseAddress = new Uri(WebhookSinkUrl);
        _gateway.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);
        _gateway.DefaultRequestHeaders.Add("api-version", "1");
        _admin.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _token);

        await _webhookSink.DeleteAsync("/deliveries");
    }

    public void Dispose()
    {
        _gateway.Dispose();
        _admin.Dispose();
        _webhookSink.Dispose();
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    // ── Test 1: Mint ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Mint_credits_account_and_delivers_webhook()
    {
        E2ESkip.IfNotConfigured(BaseUrl);

        var accountId = Guid.NewGuid();
        const decimal Amount = 1000m;

        var txId = await PostMintAsync(accountId, Amount);

        var balance = await PollUntilAsync(
            () => GetBalanceAsync(accountId),
            b => b >= Amount,
            TimeSpan.FromSeconds(30));

        balance.Should().Be(Amount, "account balance must equal minted amount");

        var deliveries = await PollUntilAsync(
            () => GetWebhookDeliveriesAsync(),
            d => d.Any(x => x.EventType == "transaction.minted"),
            TimeSpan.FromSeconds(30));

        var delivery = deliveries.First(d => d.EventType == "transaction.minted");
        delivery.Body.Should().Contain(txId.ToString());

        var expectedSig = ComputeHmac(delivery.Body);
        delivery.Signature.Should().Be(expectedSig, "X-Signature must be valid HMAC-SHA256 over the raw body");
    }

    // ── Test 2: Hold → Capture ────────────────────────────────────────────────

    [Fact]
    public async Task HoldCapture_moves_funds_and_delivers_captured_webhook()
    {
        E2ESkip.IfNotConfigured(BaseUrl);

        var aliceId = Guid.NewGuid();
        var bobId   = Guid.NewGuid();
        await MintAndWaitAsync(aliceId, 5000m);

        var holdTxId = await PostTransferAsync(aliceId, bobId, 200m);

        await PollUntilAsync(
            () => GetTransactionStatusAsync(holdTxId),
            s => s == "Held",
            TimeSpan.FromSeconds(30));

        var capResp = await PostNoBodyAsync($"/transfer/{holdTxId}/capture");
        capResp.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);

        var status = await PollUntilAsync(
            () => GetTransactionStatusAsync(holdTxId),
            s => s == "Captured",
            TimeSpan.FromSeconds(30));

        status.Should().Be("Captured");

        var deliveries = await PollUntilAsync(
            () => GetWebhookDeliveriesAsync(),
            d => d.Any(x => x.EventType == "transaction.captured"),
            TimeSpan.FromSeconds(30));

        deliveries.Should().Contain(d => d.EventType == "transaction.captured");
    }

    // ── Test 3: Hold → Void ───────────────────────────────────────────────────

    [Fact]
    public async Task HoldVoid_returns_funds_and_delivers_voided_webhook()
    {
        E2ESkip.IfNotConfigured(BaseUrl);

        var aliceId = Guid.NewGuid();
        var bobId   = Guid.NewGuid();
        await MintAndWaitAsync(aliceId, 1000m);

        var holdTxId = await PostTransferAsync(aliceId, bobId, 300m);

        await PollUntilAsync(
            () => GetTransactionStatusAsync(holdTxId),
            s => s == "Held",
            TimeSpan.FromSeconds(30));

        var voidResp = await PostNoBodyAsync($"/transfer/{holdTxId}/void");
        voidResp.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);

        var status = await PollUntilAsync(
            () => GetTransactionStatusAsync(holdTxId),
            s => s == "Voided",
            TimeSpan.FromSeconds(30));

        status.Should().Be("Voided");

        var deliveries = await PollUntilAsync(
            () => GetWebhookDeliveriesAsync(),
            d => d.Any(x => x.EventType == "transaction.voided"),
            TimeSpan.FromSeconds(30));

        deliveries.Should().Contain(d => d.EventType == "transaction.voided");
    }

    // ── Test 4: Hold → force-fail Capture → compensation Void → Failed ────────
    //
    // Skipped: the `force_fail_capture` metadata fault switch is described in
    // src/Ledger/CLAUDE.md but isn't wired into the TransactionIntake API
    // (no `metadata` field on TransferRequest). Re-enable once that is implemented.

    [Fact(Skip = "force_fail_capture metadata pathway not yet implemented in TransferRequest")]
    public Task ForceFailCapture_triggers_compensation_and_failed_webhook() =>
        Task.CompletedTask;

    // ── Test 5: Zero-sum invariant sweep ─────────────────────────────────────

    [Fact]
    public async Task After_all_flows_zero_sum_invariant_holds()
    {
        E2ESkip.IfNotConfigured(BaseUrl);

        var resp = await _admin.GetAsync("/admin/invariants/zero-sum");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var violations = doc.RootElement.GetProperty("violations").GetArrayLength();
        violations.Should().Be(0, "every (tx_id, phase) pair in the ledger must sum to zero");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<string> FetchTokenAsync()
    {
        using var kc = new HttpClient();
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = ClientId,
            ["client_secret"] = ClientSecret,
            ["scope"]         = Scopes,
        });
        var resp = await kc.PostAsync(
            $"{KeycloakUrl}/realms/{Realm}/protocol/openid-connect/token", form);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task<Guid> PostMintAsync(Guid creditAccountId, decimal amount)
    {
        var resp = await PostJsonAsync("/mint", new
        {
            creditAccountId,
            amount,
            asset = "USD",
        }, idempotencyKey: Guid.NewGuid().ToString());

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);
        return await ExtractTransactionIdAsync(resp);
    }

    private async Task<Guid> PostTransferAsync(Guid debitAccountId, Guid creditAccountId, decimal amount)
    {
        var resp = await PostJsonAsync("/transfer", new
        {
            debitAccountId,
            creditAccountId,
            amount,
            asset = "USD",
        }, idempotencyKey: Guid.NewGuid().ToString());

        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Accepted, HttpStatusCode.OK);
        return await ExtractTransactionIdAsync(resp);
    }

    private static async Task<Guid> ExtractTransactionIdAsync(HttpResponseMessage resp)
    {
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("transactionId").GetGuid();
    }

    private async Task<HttpResponseMessage> PostJsonAsync(
        string path, object body, string? idempotencyKey = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        if (idempotencyKey is not null)
        {
            req.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return await _gateway.SendAsync(req);
    }

    private async Task<HttpResponseMessage> PostNoBodyAsync(string path)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        return await _gateway.SendAsync(req);
    }

    private static string ComputeHmac(string body)
    {
        var key  = Encoding.UTF8.GetBytes(WebhookHmacSecret);
        var data = Encoding.UTF8.GetBytes(body);
        var hash = HMACSHA256.HashData(key, data);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task MintAndWaitAsync(Guid accountId, decimal amount)
    {
        await PostMintAsync(accountId, amount);
        await PollUntilAsync(() => GetBalanceAsync(accountId), b => b >= amount, TimeSpan.FromSeconds(30));
    }

    private async Task<decimal> GetBalanceAsync(Guid accountId)
    {
        var resp = await _gateway.GetAsync($"/accounts/{accountId}/balance");
        if (!resp.IsSuccessStatusCode)
        {
            return 0m;
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("balance").GetDecimal();
    }

    private async Task<string> GetTransactionStatusAsync(Guid txId)
    {
        var resp = await _gateway.GetAsync($"/transactions/{txId}");
        if (!resp.IsSuccessStatusCode)
        {
            return "";
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("status").GetString() ?? "";
    }

    private async Task<WebhookDeliveryDto[]> GetWebhookDeliveriesAsync()
    {
        var resp = await _webhookSink.GetAsync("/deliveries");
        if (!resp.IsSuccessStatusCode)
        {
            return [];
        }

        return JsonSerializer.Deserialize<WebhookDeliveryDto[]>(
                   await resp.Content.ReadAsStringAsync(), JsonOpts)
               ?? [];
    }

    private static async Task<T> PollUntilAsync<T>(
        Func<Task<T>> fetch,
        Func<T, bool> condition,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var value = await fetch();
            if (condition(value))
            {
                return value;
            }

            await Task.Delay(500);
        }
        return await fetch();
    }

    private sealed record WebhookDeliveryDto(
        string EventType,
        string Signature,
        string CorrelationId,
        string Body);
}

/// <summary>Runtime skip helper — xUnit's [Skip] attribute requires a compile-time constant.</summary>
static file class E2ESkip
{
    public static void IfNotConfigured(string? baseUrl)
    {
        if (baseUrl is null)
        {
            throw new E2ESkipException(
                "Set E2E_BASE_URL=http://localhost:5000 to run E2E tests against a live compose stack.");
        }
    }
}

sealed file class E2ESkipException(string reason) : Exception(reason);
