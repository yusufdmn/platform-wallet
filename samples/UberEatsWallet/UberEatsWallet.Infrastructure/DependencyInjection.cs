using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UberEatsWallet.Application.Abstractions;
using UberEatsWallet.Infrastructure.Persistence;
using UberEatsWallet.Infrastructure.Seeding;
using UberEatsWallet.Infrastructure.Time;
using UberEatsWallet.Infrastructure.Wallet;

namespace UberEatsWallet.Infrastructure;

public static class DependencyInjection
{
    private const string AppDbConnectionName = "AppDb";
    private const string DefaultConnectionString = "Data Source=ubereats-wallet.db";
    private const string ApiVersionHeader = "api-version";
    private const string DefaultApiVersion = "1";
    private const string DefaultAsset = "USD";
    private const string DefaultScopes = "ledger:read ledger:write";
    private const decimal DefaultOpeningBalance = 500m;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        ConfigureOptions(services, configuration);

        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite(configuration.GetConnectionString(AppDbConnectionName) ?? DefaultConnectionString));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICatalogRepository, CatalogRepository>();

        services.AddTransient<TokenAuthHandler>();
        services.AddHttpClient(TokenAuthHandler.TokenClientName);
        services.AddHttpClient<IWalletGateway, WalletGateway>(ConfigureWalletClient)
            .AddHttpMessageHandler<TokenAuthHandler>();

        services.AddHostedService<DatabaseInitializer>();
        services.AddHostedService<DemoDataSeeder>();

        return services;
    }

    private static void ConfigureWalletClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<WalletOptions>>().Value;
        client.BaseAddress = new Uri(options.GatewayUrl);
        client.DefaultRequestHeaders.Add(ApiVersionHeader, options.ApiVersion);
    }

    private static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WalletOptions>(o =>
        {
            o.GatewayUrl = Required(configuration, "WALLET_GATEWAY_URL");
            o.ApiVersion = configuration["WALLET_API_VERSION"] ?? DefaultApiVersion;
            o.Asset = configuration["WALLET_ASSET"] ?? DefaultAsset;
            o.TokenUrl = Required(configuration, "WALLET_TOKEN_URL");
            o.ClientId = Required(configuration, "WALLET_CLIENT_ID");
            o.ClientSecret = Required(configuration, "WALLET_CLIENT_SECRET");
            o.Scopes = configuration["WALLET_SCOPES"] ?? DefaultScopes;
        });

        services.Configure<SeedOptions>(o =>
            o.OpeningBalance = configuration.GetValue<decimal?>("SEED_OPENING_BALANCE") ?? DefaultOpeningBalance);
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key]
            ?? throw new InvalidOperationException($"Missing required configuration value '{key}'.");
}
