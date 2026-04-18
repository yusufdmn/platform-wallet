using PlatformWallet.Observability;

var builder = WebApplication.CreateBuilder(args);

// Observability is wired in every service via the single BuildingBlocks extension.
// Do not add homegrown OTel wiring — enforced by `otel-wiring-reviewer`.
builder.Services.AddPlatformWalletObservability(builder.Configuration, "ledger-service");

// TODO: AddAuthentication(JwtBearer), AddDbContext<LedgerDbContext>, AddMassTransit,
//       AddGrpc, endpoints. Wired during build-order step 3 in TheMainPlan.md §6.1.

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.MapGet("/", () => Results.Ok(new { service = "ledger-service", status = "scaffold" }));

app.Run();

public partial class Program;
