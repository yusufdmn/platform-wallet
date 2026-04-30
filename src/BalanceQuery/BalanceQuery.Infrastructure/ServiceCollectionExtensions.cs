using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlatformWallet.BalanceQuery.Application.Queries;
using PlatformWallet.BalanceQuery.Infrastructure.Grpc;
using PlatformWallet.Grpc.Protos;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace PlatformWallet.BalanceQuery.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBalanceQueryInfrastructure(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        AddGrpcClient(services, configuration);
        AddFusionCache(services, configuration);

        services.AddScoped<IBalanceQueryService, LedgerGrpcBalanceService>();

        return services;
    }

    private static void AddGrpcClient(IServiceCollection services, IConfiguration configuration)
    {
        var ledgerGrpcUrl = configuration["LEDGER_GRPC_URL"]
            ?? throw new InvalidOperationException("LEDGER_GRPC_URL is required");

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        services.AddGrpcClient<LedgerReader.LedgerReaderClient>(o =>
            o.Address = new Uri(ledgerGrpcUrl))
            .ConfigureChannel(o =>
            {
                o.HttpHandler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
            });
    }

    private static void AddFusionCache(IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration["REDIS_CONNECTION"]
            ?? throw new InvalidOperationException("REDIS_CONNECTION is required");

        services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);

        services.AddFusionCache()
            .WithDefaultEntryOptions(opt =>
            {
                opt.Duration                        = TimeSpan.FromSeconds(30);
                opt.DistributedCacheDuration        = TimeSpan.FromMinutes(5);
                opt.IsFailSafeEnabled               = true;
                opt.FailSafeMaxDuration             = TimeSpan.FromMinutes(30);
                opt.FailSafeThrottleDuration        = TimeSpan.FromSeconds(30);
            })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithDistributedCache(sp => sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>())
            .WithBackplane(new RedisBackplane(new RedisBackplaneOptions
            {
                Configuration = redisConnection,
            }));
    }
}
