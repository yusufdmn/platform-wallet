using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;
using PlatformWallet.Ledger.Application.Persistence;

namespace PlatformWallet.Ledger.Infrastructure.Persistence;

internal sealed class NpgsqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    public IDbConnection Create()
    {
        var host     = configuration["POSTGRES_HOST"]     ?? "postgres";
        var port     = configuration["POSTGRES_PORT"]     ?? "5432";
        var password = configuration["POSTGRES_PASSWORD"] ?? string.Empty;
        var user     = configuration["POSTGRES_USER"]     ?? "postgres";

        var connStr = $"Host={host};Port={port};Database=ledger_db;Username={user};Password={password}";
        return new NpgsqlConnection(connStr);
    }
}
