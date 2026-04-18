using PlatformWallet.Observability;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPlatformWalletObservability(builder.Configuration, "saga-orchestrator");

// TODO: AddMassTransit with AddSagaStateMachine<TransactionSagaStateMachine, ...>
//       .EntityFrameworkRepository(r => r.ConcurrencyMode = ConcurrencyMode.Pessimistic;
//                                      r.UsePostgres()),
//       UsePartitioner on CorrelationId, UseMessageRetry + UseScheduledRedelivery,
//       the five fault consumers, and admin HTTP endpoints behind ledger:admin.

var app = builder.Build();
app.Run();
