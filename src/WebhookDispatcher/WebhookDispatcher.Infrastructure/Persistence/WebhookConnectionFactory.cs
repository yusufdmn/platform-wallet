using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PlatformWallet.WebhookDispatcher.Infrastructure.Persistence;

public interface IWebhookConnectionFactory
{
    IDbConnection Create();
}

internal sealed class WebhookConnectionFactory(IConfiguration configuration) : IWebhookConnectionFactory
{
    public IDbConnection Create()
    {
        var host     = configuration["POSTGRES_HOST"]     ?? "localhost";
        var port     = configuration["POSTGRES_PORT"]     ?? "5432";
        var user     = configuration["POSTGRES_USER"]     ?? "postgres";
        var password = configuration["POSTGRES_PASSWORD"] ?? string.Empty;

        var connStr = $"Host={host};Port={port};Database=webhook_db;Username={user};Password={password}";
        return new NpgsqlConnection(connStr);
    }
}
