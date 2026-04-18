using PlatformWallet.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformWalletObservability(builder.Configuration, "transaction-intake");
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/healthz");
app.MapGet("/", () => Results.Ok(new { service = "transaction-intake", status = "scaffold" }));

app.Run();

public partial class Program;
