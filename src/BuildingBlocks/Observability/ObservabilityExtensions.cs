using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PlatformWallet.Observability;

/// <summary>
/// Single composition-root extension consumed by every service's Program.cs.
/// Wires the six auto-instrumentations referenced by TheMainPlan.md §3.5 and
/// exports OTLP to the central Collector. Services know exactly one endpoint.
/// </summary>
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
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment",
                        configuration["ASPNETCORE_ENVIRONMENT"] ?? "development")
                }))
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
}
