using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PlatformWallet.SagaOrchestrator.Infrastructure.Persistence;

public interface ISagaConnectionFactory
{
    IDbConnection Create();
}

internal sealed class SagaConnectionFactory(IConfiguration configuration) : ISagaConnectionFactory
{
    public IDbConnection Create()
    {
        var host     = configuration["POSTGRES_HOST"]     ?? "localhost";
        var port     = configuration["POSTGRES_PORT"]     ?? "5432";
        var db       = configuration["POSTGRES_SAGA_DB"]  ?? "saga_db";
        var user     = configuration["POSTGRES_USER"]     ?? "postgres";
        var password = configuration["POSTGRES_PASSWORD"] ?? string.Empty;

        var connStr = $"Host={host};Port={port};Database={db};Username={user};Password={password}";
        return new NpgsqlConnection(connStr);
    }
}
