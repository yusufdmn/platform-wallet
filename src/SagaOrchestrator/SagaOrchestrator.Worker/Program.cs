using DotNetEnv;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PlatformWallet.Observability;
using PlatformWallet.SagaOrchestrator.Domain;
using PlatformWallet.SagaOrchestrator.Infrastructure;
using PlatformWallet.SagaOrchestrator.Infrastructure.Persistence;
using PlatformWallet.SagaOrchestrator.Worker.Endpoints;
using PlatformWallet.SagaOrchestrator.Worker.ExceptionHandlers;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UsePlatformWalletLogging("saga-orchestrator");
builder.Services.AddPlatformWalletObservability(builder.Configuration, "saga-orchestrator");

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

builder.Services.AddSagaInfrastructure(builder.Configuration);

builder.Services.AddMassTransit(x =>
{
    x.AddDelayedMessageScheduler();

    x.AddSagaStateMachine<TransactionSagaStateMachine, TransactionSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
            r.UsePostgres();
            r.ExistingDbContext<SagaDbContext>();
        });

    x.AddEntityFrameworkOutbox<SagaDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        cfg.UseEntityFrameworkOutbox<SagaDbContext>(context);
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RABBITMQ_HOST"], h =>
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
            TimeSpan.FromMinutes(30)));

        cfg.UsePartitioner(8, p => p.CorrelationId
            ?? p.MessageId
            ?? throw new InvalidOperationException(
                "Messages consumed by transaction-saga must carry a CorrelationId or MessageId."));

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapSagaAdminEndpoints();
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program;
