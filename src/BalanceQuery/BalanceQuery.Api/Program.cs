using DotNetEnv;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PlatformWallet.BalanceQuery.Api.Endpoints;
using PlatformWallet.BalanceQuery.Api.ExceptionHandlers;
using PlatformWallet.BalanceQuery.Application.Consumers;
using PlatformWallet.BalanceQuery.Infrastructure;
using PlatformWallet.Observability;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformWalletLogging("balance-query");
builder.Services.AddPlatformWalletObservability(builder.Configuration, "balance-query");

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
    o.AddPolicy("ledger:read", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:read")));
    o.AddPolicy("ledger:admin", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:admin")));
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddBalanceQueryInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("balance-query", false));

    x.AddConsumer<TransactionHeldConsumer>();
    x.AddConsumer<TransactionCapturedConsumer>();
    x.AddConsumer<TransactionVoidedConsumer>();
    x.AddConsumer<TransactionMintedConsumer>();
    x.AddConsumer<TransactionBurnedConsumer>();

    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        cfg.UseInMemoryOutbox(context);
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RABBITMQ_HOST"], h =>
        {
            h.Username(builder.Configuration["RABBITMQ_DEFAULT_USER"]!);
            h.Password(builder.Configuration["RABBITMQ_DEFAULT_PASSWORD"]!);
        });

        cfg.UseMessageRetry(r =>
        {
            r.Interval(3, TimeSpan.FromSeconds(5));
            r.Ignore<ArgumentException>();
            r.Ignore<ArgumentNullException>();
            r.Ignore<NullReferenceException>();
            r.Ignore<InvalidCastException>();
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapBalanceEndpoint();
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program;
