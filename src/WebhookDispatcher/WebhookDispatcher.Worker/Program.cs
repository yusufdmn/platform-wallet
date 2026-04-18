using PlatformWallet.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPlatformWalletObservability(builder.Configuration, "webhook-dispatcher");

// TODO: AddMassTransit (consumers for TransactionCaptured|Voided|Failed|Minted|Burned),
//       UseScheduledRedelivery(1m, 5m, 30m, 2h, 12h, 24h), AddDbContext<WebhookDbContext>,
//       AddHttpClient with Polly pipeline, HmacSigner DI.

var app = builder.Build();
app.Run();
