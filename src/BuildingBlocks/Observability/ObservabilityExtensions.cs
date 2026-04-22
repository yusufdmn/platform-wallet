using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace PlatformWallet.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddPlatformWalletObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? "http://otel-collector:4317";

        services
            .AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(serviceName: serviceName, serviceVersion: "0.1.0")
                .AddAttributes([
                    new("deployment.environment",
                        configuration["ASPNETCORE_ENVIRONMENT"] ?? "development")
                ]))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation(o => o.SetDbStatementForText = true)
                .AddRedisInstrumentation()
                .AddSource("MassTransit")
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return services;
    }

    public static IHostBuilder UsePlatformWalletLogging(
        this IHostBuilder host,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        return host.UseSerilog((ctx, _, cfg) =>
        {
            var seqUrl = ctx.Configuration["SEQ_URL"] ?? "http://seq:5341";

            cfg
                .ReadFrom.Configuration(ctx.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("ServiceName", serviceName)
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {ServiceName} | {Message:lj}{NewLine}{Exception}")
                .WriteTo.Seq(seqUrl);
        });
    }
}
