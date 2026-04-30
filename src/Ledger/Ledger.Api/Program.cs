using DotNetEnv;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using PlatformWallet.Ledger.Api.Endpoints;
using PlatformWallet.Ledger.Application.Consumers;
using PlatformWallet.Ledger.Application.GrpcServices;
using PlatformWallet.Ledger.Infrastructure;
using PlatformWallet.Ledger.Infrastructure.Persistence;
using PlatformWallet.Observability;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Port 14038 = HTTP/1.1 for REST (health, admin).
// Port 14099 = HTTP/2 h2c for gRPC (Http1AndHttp2 without TLS falls back to HTTP/1.1 only).
builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenLocalhost(14038, ep => ep.Protocols = HttpProtocols.Http1);
    o.ListenLocalhost(14099, ep => ep.Protocols = HttpProtocols.Http2);
});

builder.Host.UsePlatformWalletLogging("ledger-service");
builder.Services.AddPlatformWalletObservability(builder.Configuration, "ledger-service");

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
    o.AddPolicy("ledger:read",  p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:read")));
    o.AddPolicy("ledger:admin", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:admin")));
});

builder.Services.AddLedgerInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MintFundsConsumer>();
    x.AddConsumer<HoldFundsConsumer>();
    x.AddConsumer<CaptureTransferConsumer>();
    x.AddConsumer<VoidHoldConsumer>();

    x.AddEntityFrameworkOutbox<LedgerDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RABBITMQ_HOST"], h =>
        {
            h.Username(builder.Configuration["RABBITMQ_DEFAULT_USER"]!);
            h.Password(builder.Configuration["RABBITMQ_DEFAULT_PASSWORD"]!);
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddGrpc();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<LedgerGrpcService>();
app.MapInvariantEndpoint();
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program;
