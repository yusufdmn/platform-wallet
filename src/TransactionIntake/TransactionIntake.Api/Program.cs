using DotNetEnv;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PlatformWallet.Observability;
using PlatformWallet.TransactionIntake.Api.Endpoints;
using PlatformWallet.TransactionIntake.Application.Behaviours;
using PlatformWallet.TransactionIntake.Application.Commands.SubmitMint;
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
    o.AddPolicy("ledger:write", p => p.RequireClaim("scope", "ledger:write")));

builder.Services.AddIntakeInfrastructure(configuration);

builder.Services.AddValidatorsFromAssemblyContaining<SubmitMintValidator>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<SubmitMintHandler>();
    cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
});

builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<IntakeDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
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
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program;
