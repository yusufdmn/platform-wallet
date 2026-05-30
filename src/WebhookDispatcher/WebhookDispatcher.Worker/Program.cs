using DotNetEnv;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Observability;
using PlatformWallet.WebhookDispatcher.Application.Consumers;
using PlatformWallet.WebhookDispatcher.Infrastructure;
using PlatformWallet.WebhookDispatcher.Worker.Endpoints;
using PlatformWallet.WebhookDispatcher.Worker.ExceptionHandlers;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformWalletLogging("webhook-dispatcher");
builder.Services.AddPlatformWalletObservability(builder.Configuration, "webhook-dispatcher");

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
    o.AddPolicy("ledger:admin", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:admin")));
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddWebhookInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("webhook", false));

    x.AddDelayedMessageScheduler();

    x.AddConsumer<TransactionMintedConsumer>();
    x.AddConsumer<TransactionBurnedConsumer>();
    x.AddConsumer<TransactionCapturedConsumer>();
    x.AddConsumer<TransactionVoidedConsumer>();
    x.AddConsumer<TransactionFailedConsumer>();

    x.AddConsumer<WebhookFaultConsumer<TransactionMinted>>();
    x.AddConsumer<WebhookFaultConsumer<TransactionBurned>>();
    x.AddConsumer<WebhookFaultConsumer<TransactionCaptured>>();
    x.AddConsumer<WebhookFaultConsumer<TransactionVoided>>();
    x.AddConsumer<WebhookFaultConsumer<TransactionFailed>>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        var rabbitHost = builder.Configuration["RABBITMQ_HOST"]
            ?? throw new InvalidOperationException("RABBITMQ_HOST is required");
        var rabbitPort = ushort.TryParse(builder.Configuration["RABBITMQ_PORT"], out var p) ? p : (ushort)5672;

        cfg.Host(rabbitHost, rabbitPort, "/", h =>
        {
            h.Username(builder.Configuration["RABBITMQ_DEFAULT_USER"]!);
            h.Password(builder.Configuration["RABBITMQ_DEFAULT_PASSWORD"]!);
        });

        cfg.UseDelayedMessageScheduler();

        cfg.UseMessageRetry(r =>
        {
            r.Intervals(
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(2));
            r.Ignore<ArgumentException>();
            r.Ignore<ArgumentNullException>();
            r.Ignore<NullReferenceException>();
            r.Ignore<InvalidCastException>();
        });

        cfg.UseScheduledRedelivery(r => r.Intervals(
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(12),
            TimeSpan.FromHours(24)));

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapWebhookAdminEndpoints();
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program;
