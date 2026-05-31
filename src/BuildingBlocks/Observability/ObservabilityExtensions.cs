using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

namespace PlatformWallet.Observability;

public static class ObservabilityExtensions
{
    private const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string DefaultOtlpEndpoint = "http://otel-collector:4317";
    private const string EnvironmentKey = "ASPNETCORE_ENVIRONMENT";
    private const string DefaultEnvironment = "development";
    private const string ServiceVersion = "0.1.0";

    public static IServiceCollection AddPlatformWalletObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var otlpEndpoint = ResolveOtlpEndpoint(configuration);

        services
            .AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(serviceName: serviceName, serviceVersion: ServiceVersion)
                .AddAttributes([
                    new("deployment.environment", ResolveEnvironment(configuration))
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
            var otlpEndpoint = ResolveOtlpEndpoint(ctx.Configuration);

            cfg
                .ReadFrom.Configuration(ctx.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProperty("ServiceName", serviceName)
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {ServiceName} | {Message:lj}{NewLine}{Exception}")
                // Logs flow over OTLP to the OTel Collector, which fans them out
                // to Seq (and any other log backend). Services never talk to Seq directly.
                .WriteTo.OpenTelemetry(o =>
                {
                    o.Endpoint = otlpEndpoint;
                    o.Protocol = OtlpProtocol.Grpc;
                    o.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = serviceName,
                        ["service.version"] = ServiceVersion,
                        ["deployment.environment"] = ResolveEnvironment(ctx.Configuration)
                    };
                });
        });
    }

    private static string ResolveOtlpEndpoint(IConfiguration configuration) =>
        configuration[OtlpEndpointKey] ?? DefaultOtlpEndpoint;

    private static string ResolveEnvironment(IConfiguration configuration) =>
        configuration[EnvironmentKey] ?? DefaultEnvironment;
}
