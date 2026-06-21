using System.Net.Http.Headers;
using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PlatformWallet.ApiGateway.Yarp.Endpoints;
using PlatformWallet.ApiGateway.Yarp.ExceptionHandlers;
using PlatformWallet.ApiGateway.Yarp.Infrastructure.Rabbit;
using PlatformWallet.ApiGateway.Yarp.Infrastructure.RateLimiting;
using PlatformWallet.ApiGateway.Yarp.Middleware;
using PlatformWallet.Observability;
using StackExchange.Redis;

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
    o.AddPolicy("LedgerWrite", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:write")));
    o.AddPolicy("LedgerRead",  p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:read")));
    o.AddPolicy("LedgerAdmin", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:admin")));
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var redisConnection = builder.Configuration["REDIS_CONNECTION"]
    ?? throw new InvalidOperationException("REDIS_CONNECTION is required");

// One shared multiplexer backs both the idempotency cache and the rate limiter.
// AbortOnConnectFail=false keeps startup non-blocking when Redis is briefly unavailable
// (it reconnects in the background) and underpins the rate limiter's fail-open behaviour
// (see FailOpenRateLimiter). A short connect timeout bounds how long a degraded Redis can
// stall the first requests.
var redisOptions = ConfigurationOptions.Parse(redisConnection);
redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectTimeout     = 2000;
redisOptions.ConnectRetry       = 1;
var redisMultiplexer = await ConnectionMultiplexer.ConnectAsync(redisOptions);
builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);

builder.Services.AddStackExchangeRedisCache(o =>
    o.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redisMultiplexer));

// Distributed, Redis-backed token-bucket rate limit partitioned by remote IP.
builder.Services.AddGatewayRateLimiting(
    redisMultiplexer,
    GatewayRateLimitOptions.FromConfiguration(builder.Configuration));

var rabbitMgmt = new RabbitMqManagementOptions
{
    BaseUrl  = builder.Configuration["RABBITMQ_MGMT_URL"]
               ?? throw new InvalidOperationException("RABBITMQ_MGMT_URL is required"),
    Username = builder.Configuration["RABBITMQ_DEFAULT_USER"] ?? "",
    Password = builder.Configuration["RABBITMQ_DEFAULT_PASSWORD"] ?? "",
    Vhost    = builder.Configuration["RABBITMQ_VHOST"] ?? "/",
};
builder.Services.AddSingleton(rabbitMgmt);
builder.Services.AddHttpClient<IRabbitMqManagementClient, RabbitMqManagementClient>(client =>
{
    client.BaseAddress = new Uri(rabbitMgmt.BaseUrl.TrimEnd('/') + "/");
    var token = Convert.ToBase64String(
        Encoding.UTF8.GetBytes($"{rabbitMgmt.Username}:{rabbitMgmt.Password}"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

// Confine the admin plane (/console + /admin) to the internal listener. Runs first, ahead
// of static files and auth, so the public listener 404s those paths before serving anything.
app.UseMiddleware<AdminPlaneGuardMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseExceptionHandler();

// Middleware order per CLAUDE.md: Auth → ScopePolicy → RateLimit → Idempotency → CorrelationId → YARP
app.UseAuthentication();
app.UseAuthorization();
// Rate limiting disabled on the load-test branch so throughput stress reaches the
// services instead of being capped at the gateway. Re-enable for production.
// app.UseRateLimiter();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/healthz");
app.MapConsoleConfigEndpoint();
app.MapDlqAdminEndpoints();
app.MapReverseProxy();

app.Run();

public partial class Program;
