using MassTransit;
using PlatformWallet.Observability;
using PlatformWallet.SagaOrchestrator.Domain;
using PlatformWallet.SagaOrchestrator.Infrastructure;
using PlatformWallet.SagaOrchestrator.Infrastructure.Persistence;

DotNetEnv.Env.TraversePath().Load();

var host = Host.CreateDefaultBuilder(args)
    .UsePlatformWalletLogging("saga-orchestrator")
    .ConfigureServices((ctx, services) =>
    {
        var configuration = ctx.Configuration;

        services.AddPlatformWalletObservability(configuration, "saga-orchestrator");
        services.AddSagaInfrastructure(configuration);

        services.AddMassTransit(x =>
        {
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

            x.UsingRabbitMq((_, cfg) =>
            {
                cfg.Host(configuration["RABBITMQ_HOST"], h =>
                {
                    h.Username(configuration["RABBITMQ_DEFAULT_USER"]!);
                    h.Password(configuration["RABBITMQ_DEFAULT_PASSWORD"]!);
                });

                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(2)));

                // UseScheduledRedelivery requires RabbitMQ delayed-message exchange plugin or
                // a Quartz/Hangfire scheduler — not available in this deployment.
                // UseMessageRetry above is sufficient for transient saga faults.

                cfg.UsePartitioner(8, p => p.CorrelationId
                    ?? p.MessageId
                    ?? throw new InvalidOperationException(
                        "Messages consumed by transaction-saga must carry a CorrelationId or MessageId."));

                cfg.ConfigureEndpoints(_);
            });
        });
    })
    .Build();

host.Run();

public partial class Program;
