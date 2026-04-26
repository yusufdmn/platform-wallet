using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using PlatformWallet.WebhookDispatcher.Application.Services;
using PlatformWallet.WebhookDispatcher.Infrastructure.Configuration;
using PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;
using PlatformWallet.WebhookDispatcher.Infrastructure.Services;

namespace PlatformWallet.WebhookDispatcher.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebhookInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgresHost     = configuration["POSTGRES_HOST"]     ?? "localhost";
        var postgresPort     = configuration["POSTGRES_PORT"]     ?? "5432";
        var postgresUser     = configuration["POSTGRES_USER"]     ?? "postgres";
        var postgresPassword = configuration["POSTGRES_PASSWORD"] ?? string.Empty;

        var connectionString =
            $"Host={postgresHost};Port={postgresPort};Database=webhook_db;Username={postgresUser};Password={postgresPassword}";

        services.AddDbContext<WebhookDbContext>(o =>
            o.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

        services.AddHostedService<DatabaseMigratorService>();

        var targetUrl  = configuration["WEBHOOK_TARGET_URL"]
            ?? throw new InvalidOperationException("WEBHOOK_TARGET_URL is required");
        var hmacSecret = configuration["WEBHOOK_HMAC_SECRET"]
            ?? throw new InvalidOperationException("WEBHOOK_HMAC_SECRET is required");

        services.Configure<WebhookOptions>(o =>
        {
            o.TargetUrl  = targetUrl;
            o.HmacSecret = hmacSecret;
        });

        services.AddHttpClient("webhook", c => c.BaseAddress = new Uri(targetUrl))
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts       = 3;
                o.Retry.Delay                  = TimeSpan.FromMilliseconds(500);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            });

        services.AddScoped<IWebhookDeliveryService, WebhookDeliveryService>();
        services.AddScoped<IFailedDeliveryRepository, FailedDeliveryRepository>();

        return services;
    }
}
