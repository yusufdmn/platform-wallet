using DotNetEnv;
using MassTransit;
using PlatformWallet.Contracts.Events;
using PlatformWallet.Observability;
using PlatformWallet.WebhookDispatcher.Application.Consumers;
using PlatformWallet.WebhookDispatcher.Infrastructure;

Env.TraversePath().Load();

var host = Host.CreateDefaultBuilder(args)
    .UsePlatformWalletLogging("webhook-dispatcher")
    .ConfigureServices((ctx, services) =>
    {
        var configuration = ctx.Configuration;

        services.AddPlatformWalletObservability(configuration, "webhook-dispatcher");
        services.AddWebhookInfrastructure(configuration);

        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("webhook", false));

            x.AddConsumer<TransactionMintedConsumer>();
            x.AddConsumer<TransactionCapturedConsumer>();
            x.AddConsumer<TransactionVoidedConsumer>();
            x.AddConsumer<TransactionFailedConsumer>();

            x.AddConsumer<WebhookFaultConsumer<TransactionMinted>>();
            x.AddConsumer<WebhookFaultConsumer<TransactionCaptured>>();
            x.AddConsumer<WebhookFaultConsumer<TransactionVoided>>();
            x.AddConsumer<WebhookFaultConsumer<TransactionFailed>>();

            x.UsingRabbitMq((_, cfg) =>
            {
                var rabbitHost = configuration["RABBITMQ_HOST"]
                    ?? throw new InvalidOperationException("RABBITMQ_HOST is required");
                var rabbitPort = ushort.TryParse(configuration["RABBITMQ_PORT"], out var p) ? p : (ushort)5672;

                cfg.Host(rabbitHost, rabbitPort, "/", h =>
                {
                    h.Username(configuration["RABBITMQ_DEFAULT_USER"]!);
                    h.Password(configuration["RABBITMQ_DEFAULT_PASSWORD"]!);
                });

                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromMilliseconds(100),
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(2)));

                cfg.UseScheduledRedelivery(r => r.Intervals(
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(2),
                    TimeSpan.FromHours(12),
                    TimeSpan.FromHours(24)));

                cfg.ConfigureEndpoints(_);
            });
        });
    })
    .Build();

host.Run();

public partial class Program;
