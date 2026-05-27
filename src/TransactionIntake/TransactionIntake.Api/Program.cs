using DotNetEnv;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PlatformWallet.Observability;
using PlatformWallet.TransactionIntake.Api.Endpoints;
using PlatformWallet.TransactionIntake.Application.Behaviours;
using PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;
using PlatformWallet.TransactionIntake.Application.Consumers;
using PlatformWallet.TransactionIntake.Infrastructure;
using PlatformWallet.TransactionIntake.Infrastructure.Persistence;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.Host.UsePlatformWalletLogging("transaction-intake");
builder.Services.AddPlatformWalletObservability(configuration, "transaction-intake");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.Authority            = configuration["KEYCLOAK_AUTHORITY"];
        o.Audience             = "platform-wallet-api";
        o.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("ledger:write", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:write")));
    o.AddPolicy("ledger:read",  p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:read")));
    o.AddPolicy("ledger:admin", p => p.RequireAssertion(ctx =>
        (ctx.User.FindFirst("scope")?.Value ?? "").Split(' ').Contains("ledger:admin")));
});

builder.Services.AddIntakeInfrastructure(configuration);

builder.Services.AddValidatorsFromAssemblyContaining<SubmitMintValidator>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<SubmitMintHandler>();
    cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
});

builder.Services.AddMassTransit(x =>
{
    x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("intake", false));

    x.AddConsumer<TransactionHeldConsumer>();
    x.AddConsumer<TransactionMintedConsumer>();
    x.AddConsumer<TransactionCapturedConsumer>();
    x.AddConsumer<TransactionVoidedConsumer>();
    x.AddConsumer<TransactionFailedConsumer>();

    x.AddEntityFrameworkOutbox<IntakeDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        cfg.UseEntityFrameworkOutbox<IntakeDbContext>(context);
    });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(configuration["RABBITMQ_HOST"], h =>
        {
            h.Username(configuration["RABBITMQ_DEFAULT_USER"]!);
            h.Password(configuration["RABBITMQ_DEFAULT_PASSWORD"]!);
        });
        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMintEndpoint();
app.MapTransferEndpoint();
app.MapCaptureEndpoint();
app.MapVoidEndpoint();
app.MapGetTransactionEndpoint();
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program;
