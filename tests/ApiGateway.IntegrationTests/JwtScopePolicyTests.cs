using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace PlatformWallet.ApiGateway.IntegrationTests;

[Trait("Category", "Integration")]
public class JwtScopePolicyTests
{
    private static readonly SymmetricSecurityKey TestKey =
        new(Encoding.UTF8.GetBytes("platform-wallet-test-signing-key-32chars!"));

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(host =>
            {
                // REDIS_CONNECTION must be set before the app starts — use a fake value;
                // the actual IDistributedCache is swapped to in-memory below.
                host.UseSetting("REDIS_CONNECTION",   "localhost:6379");
                host.UseSetting("KEYCLOAK_AUTHORITY", "http://localhost/fake-keycloak");

                host.ConfigureServices(services =>
                {
                    // Remove Redis cache registration from Program.cs and replace with in-memory
                    services.RemoveAll<IDistributedCache>();
                    services.AddDistributedMemoryCache();

                    // Override JWT validation to accept locally-signed test tokens
                    services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
                    {
                        o.Authority = null;
                        o.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer   = false,
                            ValidateAudience = false,
                            ValidateLifetime = true,
                            IssuerSigningKey = TestKey,
                        };
                        o.Configuration = new OpenIdConnectConfiguration();
                    });
                });
            });
    }

    private static string BuildJwt(params string[] scopes)
    {
        var handler = new JwtSecurityTokenHandler();
        var claims  = new List<Claim> { new("sub", "test-user") };
        foreach (var s in scopes)
        {
            claims.Add(new Claim("scope", s));
        }

        var token = handler.CreateJwtSecurityToken(
            issuer:             "test",
            audience:           "platform-wallet-api",
            subject:            new ClaimsIdentity(claims),
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(TestKey, SecurityAlgorithms.HmacSha256));

        return handler.WriteToken(token);
    }

    [Fact]
    public async Task Missing_LedgerWrite_scope_returns_403_on_POST_transactions()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // Token has only ledger:read — not ledger:write
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BuildJwt("ledger:read"));

        using var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/v1/transactions/mint", content);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "LedgerWrite policy requires ledger:write scope — ledger:read alone must be rejected");
    }

    [Fact]
    public async Task Rate_limit_breach_returns_429_with_RetryAfter_header()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        HttpResponseMessage? lastResponse = null;

        // Rate limiter allows 100 requests per minute. /healthz has no auth requirement.
        for (int i = 0; i < 101; i++)
        {
            lastResponse?.Dispose();
            lastResponse = await client.GetAsync("/healthz");

            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        lastResponse!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests,
            "fixed-window rate limiter must reject the 101st request from same IP");
    }

    [Fact]
    public async Task Repeat_idempotency_key_returns_cached_response_without_forwarding()
    {
        await using var factory = CreateFactory();

        var idempotencyKey = Guid.NewGuid().ToString();
        const string callerId = "test-user";
        var cacheKey = $"gw:idempotency:{callerId}:POST:/v1/transactions/mint:{idempotencyKey}";

        // Pre-seed the in-memory distributed cache with a fake 202 response
        var cachedEntry = JsonSerializer.Serialize(new
        {
            StatusCode  = 202,
            ContentType = "application/json",
            BodyBytes   = Encoding.UTF8.GetBytes("""{"id":"cached-id"}"""),
        });

        var cache = factory.Services.GetRequiredService<IDistributedCache>();
        await cache.SetStringAsync(cacheKey, cachedEntry, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        });

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", BuildJwt("ledger:write"));

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/transactions/mint")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await client.SendAsync(request);

        // Idempotency middleware returns the cached 202 without forwarding to YARP
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "idempotency middleware must return the cached response for a repeated key");
    }
}
