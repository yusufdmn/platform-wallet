using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformWallet.SagaOrchestrator.Domain;
using PlatformWallet.SagaOrchestrator.Infrastructure.Persistence;

namespace PlatformWallet.SagaOrchestrator.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSagaInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connStr = BuildConnectionString(configuration);

        services.AddDbContext<SagaDbContext>(opt =>
            opt.UseNpgsql(connStr, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 5)));
        services.AddHostedService<DatabaseMigratorService>();

        services.AddSingleton(new SagaOptions
        {
            HoldTtlSeconds = int.TryParse(configuration["HoldTtlSeconds"], out var ttl) && ttl > 0
                ? ttl
                : SagaOptions.DefaultHoldTtlSeconds,
        });

        return services;
    }

    private static string BuildConnectionString(IConfiguration cfg)
    {
        var host     = cfg["POSTGRES_HOST"]     ?? "localhost";
        var port     = cfg["POSTGRES_PORT"]     ?? "5432";
        var db       = cfg["POSTGRES_SAGA_DB"]  ?? "saga_db";
        var user     = cfg["POSTGRES_USER"]     ?? "postgres";
        var password = cfg["POSTGRES_PASSWORD"] ?? throw new InvalidOperationException("POSTGRES_PASSWORD is required");

        return $"Host={host};Port={port};Database={db};Username={user};Password={password}";
    }
}
