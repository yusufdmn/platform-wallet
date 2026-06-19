using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.Redis;
using Xunit;

namespace PlatformWallet.ApiGateway.IntegrationTests;

// The rate limiter is now Redis-backed, so enforcement requires a real Redis. A small bucket
// (5 tokens, replenished once per minute) makes the breach deterministic within a fast burst:
// the first 5 requests drain the bucket and the 6th is rejected before any replenishment.
[Trait("Category", "Integration")]
public sealed class RateLimitTests : IAsyncLifetime
{
    private const int TokenLimit = 5;

    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                host.UseSetting("REDIS_CONNECTION",   _redis.GetConnectionString());
                host.UseSetting("KEYCLOAK_AUTHORITY", "http://localhost/fake-keycloak");

                host.UseSetting("RATE_LIMIT_TOKEN_LIMIT",       TokenLimit.ToString());
                host.UseSetting("RATE_LIMIT_TOKENS_PER_PERIOD", "1");
                host.UseSetting("RATE_LIMIT_REPLENISH_SECONDS", "60");
            });
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task Rate_limit_breach_returns_429_with_RetryAfter_header()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        HttpResponseMessage? lastResponse = null;

        // /healthz needs no auth. The bucket holds TokenLimit tokens; the next request is rejected.
        for (var i = 0; i < TokenLimit + 1; i++)
        {
            lastResponse?.Dispose();
            lastResponse = await client.GetAsync("/healthz");

            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        using var finalResponse = lastResponse!;

        finalResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "the Redis token bucket must reject the request that drains the last token from same IP");
        finalResponse.Headers.Contains("Retry-After").Should().BeTrue(
            "a throttled response must tell the caller when to retry");
    }
}
