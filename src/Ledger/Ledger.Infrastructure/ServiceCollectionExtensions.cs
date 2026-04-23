using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformWallet.Ledger.Application.Persistence;
using PlatformWallet.Ledger.Infrastructure.Persistence;
using PlatformWallet.Ledger.Infrastructure.Persistence.Dapper;

namespace PlatformWallet.Ledger.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLedgerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var host     = configuration["POSTGRES_HOST"]     ?? "postgres";
        var port     = configuration["POSTGRES_PORT"]     ?? "5432";
        var user     = configuration["POSTGRES_USER"]     ?? "postgres";
        var password = configuration["POSTGRES_PASSWORD"] ?? string.Empty;
        var connStr  = $"Host={host};Port={port};Database=ledger_db;Username={user};Password={password}";

        services.AddDbContext<LedgerDbContext>(o =>
            o.UseNpgsql(connStr, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 5)));

        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IAccountQueries, AccountQueries>();

        services.AddHostedService<DatabaseMigratorService>();
        services.AddHostedService<SystemAccountSeeder>();

        return services;
    }
}
