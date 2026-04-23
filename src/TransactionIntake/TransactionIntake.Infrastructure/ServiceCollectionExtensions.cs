using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformWallet.TransactionIntake.Application.Persistence;
using PlatformWallet.TransactionIntake.Infrastructure.Persistence;

namespace PlatformWallet.TransactionIntake.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIntakeInfrastructure(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        services.AddDbContext<IntakeDbContext>(o =>
            o.UseNpgsql(
                BuildConnectionString(configuration),
                npgsql => npgsql.EnableRetryOnFailure()));

        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddHostedService<DatabaseMigratorService>();

        return services;
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        var host     = configuration["POSTGRES_HOST"]     ?? "localhost";
        var port     = configuration["POSTGRES_PORT"]     ?? "5432";
        var user     = configuration["POSTGRES_USER"]     ?? "postgres";
        var password = configuration["POSTGRES_PASSWORD"] ?? "postgres";
        return $"Host={host};Port={port};Database=intake_db;Username={user};Password={password}";
    }
}
