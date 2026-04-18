using PlatformWallet.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformWalletObservability(builder.Configuration, "api-gateway");
builder.Services.AddHealthChecks();

// YARP + middleware (Auth -> ScopePolicy -> RateLimit -> Idempotency -> CorrelationId -> forward)
// wired during build-order step 9 in TheMainPlan.md §6.1.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(); // serves /console from wwwroot/console/index.html

app.MapHealthChecks("/healthz");
app.MapReverseProxy();

app.Run();

public partial class Program;
