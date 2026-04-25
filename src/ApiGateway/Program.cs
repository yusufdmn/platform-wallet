using System.Threading.RateLimiting;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using PlatformWallet.ApiGateway.Yarp.Middleware;
using PlatformWallet.Observability;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformWalletLogging("api-gateway");
builder.Services.AddPlatformWalletObservability(builder.Configuration, "api-gateway");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority            = builder.Configuration["KEYCLOAK_AUTHORITY"];
        o.Audience             = "platform-wallet-api";
        o.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("LedgerWrite", p => p.RequireClaim("scope", "ledger:write"));
    o.AddPolicy("LedgerRead",  p => p.RequireClaim("scope", "ledger:read"));
    o.AddPolicy("LedgerAdmin", p => p.RequireClaim("scope", "ledger:admin"));
});

// Fixed-window rate limit partitioned by remote IP
builder.Services.AddRateLimiter(o =>
{
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window      = TimeSpan.FromMinutes(1),
        });
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var redisConnection = builder.Configuration["REDIS_CONNECTION"]
    ?? throw new InvalidOperationException("REDIS_CONNECTION is required");

builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Middleware order per CLAUDE.md: Auth → ScopePolicy → RateLimit → Idempotency → CorrelationId → YARP
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/healthz");
app.MapReverseProxy();

app.Run();

public partial class Program;
