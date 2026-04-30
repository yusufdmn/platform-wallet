using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using PlatformWallet.BalanceQuery.Api.Endpoints;
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

builder.Services.AddBalanceQueryInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapBalanceEndpoint();
app.MapHealthChecks("/healthz");

app.Run();

public partial class Program;
