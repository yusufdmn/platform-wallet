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

builder.WebHost.ConfigureKestrel(o =>
{
    o.ListenAnyIP(8080, e => e.Protocols = HttpProtocols.Http1);   // REST + healthcheck
    o.ListenAnyIP(9090, e => e.Protocols = HttpProtocols.Http2);   // gRPC (h2c)
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
