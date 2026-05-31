using System.Net.Http.Headers;
using System.Text;
using System.Threading.RateLimiting;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using PlatformWallet.ApiGateway.Yarp.Endpoints;
using PlatformWallet.ApiGateway.Yarp.ExceptionHandlers;
using PlatformWallet.ApiGateway.Yarp.Infrastructure.Rabbit;
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
    o.AddPolicy("LedgerWrite", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:write")));
    o.AddPolicy("LedgerRead",  p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:read")));
    o.AddPolicy("LedgerAdmin", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:admin")));
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Fixed-window rate limit partitioned by remote IP
var rateLimit = int.TryParse(builder.Configuration["RATE_LIMIT_PER_MINUTE"], out var rl) ? rl : 100;
builder.Services.AddRateLimiter(o =>
{
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimit,
            Window      = TimeSpan.FromMinutes(1),
        });
    });
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var redisConnection = builder.Configuration["REDIS_CONNECTION"]
    ?? throw new InvalidOperationException("REDIS_CONNECTION is required");

builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);

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
app.UseRateLimiter();
app.UseMiddleware<IdempotencyMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapHealthChecks("/healthz");
app.MapConsoleConfigEndpoint();
app.MapDlqAdminEndpoints();
app.MapReverseProxy();

app.Run();

public partial class Program;
