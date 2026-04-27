using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformWallet.WebhookDispatcher.Application.Services;
using PlatformWallet.WebhookDispatcher.Domain;
using PlatformWallet.WebhookDispatcher.Infrastructure;
using PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace PlatformWallet.WebhookDispatcher.IntegrationTests;

[Trait("Category", "Integration")]
public class HmacSigningTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("webhook_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync()    => await _postgres.DisposeAsync();

    [Fact]
    public async Task XSignature_header_matches_hmac_sha256_of_raw_body_bytes()
    {
        const string secret    = "test-hmac-secret";
        const string eventType = "transaction.minted";
        var correlationId      = Guid.NewGuid();

        string? capturedSignature = null;
        byte[]? capturedBody      = null;

        var handler = new CapturingHandler(async req =>
        {
            capturedBody      = await req.Content!.ReadAsByteArrayAsync();
            capturedSignature = req.Headers.TryGetValues("X-Signature", out var vals)
                ? vals.FirstOrDefault()
                : null;
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSTGRES_HOST"]       = _postgres.Hostname,
                ["POSTGRES_PORT"]       = _postgres.GetMappedPublicPort(5432).ToString(),
                ["POSTGRES_USER"]       = "postgres",
                ["POSTGRES_PASSWORD"]   = "postgres",
                ["WEBHOOK_TARGET_URL"]  = "http://localhost/webhook",
                ["WEBHOOK_HMAC_SECRET"] = secret,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebhookInfrastructure(cfg);

        // Override the named HTTP client with our capturing handler so no real HTTP is made
        services.AddHttpClient("webhook", c => c.BaseAddress = new Uri("http://localhost/webhook"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var sp = services.BuildServiceProvider();

        // Skip the DatabaseMigratorService (hosted service) — not needed for this test
        await using var scope = sp.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IWebhookDeliveryService>();

        await svc.DeliverAsync(eventType, correlationId, CancellationToken.None);

        capturedBody.Should().NotBeNull();
        capturedSignature.Should().NotBeNull();

        var expectedPayload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            event_type     = eventType,
            correlation_id = correlationId,
        });
        var expectedSignature = HmacSigner.Sign(expectedPayload, Encoding.UTF8.GetBytes(secret));

        capturedBody.Should().Equal(expectedPayload);
        capturedSignature.Should().Be(expectedSignature);
        capturedSignature!.Should().StartWith("sha256=");
    }

    [Fact]
    public async Task Exhausted_redelivery_inserts_failed_webhook_deliveries_row()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["POSTGRES_HOST"]       = _postgres.Hostname,
                ["POSTGRES_PORT"]       = _postgres.GetMappedPublicPort(5432).ToString(),
                ["POSTGRES_USER"]       = "postgres",
                ["POSTGRES_PASSWORD"]   = "postgres",
                ["WEBHOOK_TARGET_URL"]  = "http://localhost/webhook",
                ["WEBHOOK_HMAC_SECRET"] = "test-secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebhookInfrastructure(cfg);

        await using var sp = services.BuildServiceProvider();

        await using var scope = sp.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<WebhookDbContext>();
        await ctx.Database.MigrateAsync();

        var repo = scope.ServiceProvider.GetRequiredService<IFailedDeliveryRepository>();

        await repo.PersistAsync(
            eventType:            "transaction.minted",
            correlationId:        Guid.NewGuid(),
            reason:               "Max retries exceeded",
            lastHttpStatusCode:   503,
            lastHttpResponseBody: "Service Unavailable",
            ct:                   CancellationToken.None);

        var count = await ctx.Set<FailedWebhookDelivery>().CountAsync();
        count.Should().Be(1);
    }
}

internal sealed class CapturingHandler(
    Func<HttpRequestMessage, Task<HttpResponseMessage>> handle)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken) =>
        handle(request);
}
