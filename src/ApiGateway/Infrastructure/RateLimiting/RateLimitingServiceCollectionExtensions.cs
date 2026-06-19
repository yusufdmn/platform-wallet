using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using RedisRateLimiting;
using RedisRateLimiting.AspNetCore;
using StackExchange.Redis;

namespace PlatformWallet.ApiGateway.Yarp.Infrastructure.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    private const string UnknownPartitionKey = "unknown";

    /// <summary>
    /// Registers a distributed, Redis-backed token-bucket rate limiter partitioned by remote IP.
    /// State lives in Redis so the limit is enforced consistently across gateway replicas, and
    /// the limiter fails open if Redis is unreachable. Rejections carry the standard
    /// <c>Retry-After</c> and <c>X-RateLimit-*</c> headers.
    /// </summary>
    public static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services,
        IConnectionMultiplexer multiplexer,
        GatewayRateLimitOptions options)
    {
        services.AddRateLimiter(limiter =>
        {
            limiter.GlobalLimiter = BuildGlobalLimiter(multiplexer, options);
            limiter.OnRejected = (context, token) =>
                RateLimitMetadata.OnRejected(context.HttpContext, context.Lease, token);
        });

        return services;
    }

    private static PartitionedRateLimiter<HttpContext> BuildGlobalLimiter(
        IConnectionMultiplexer multiplexer,
        GatewayRateLimitOptions options)
    {
        var redisLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? UnknownPartitionKey;

            return RedisRateLimitPartition.GetTokenBucketRateLimiter(partitionKey, _ =>
                new RedisTokenBucketRateLimiterOptions
                {
                    ConnectionMultiplexerFactory = () => multiplexer,
                    TokenLimit          = options.TokenLimit,
                    TokensPerPeriod     = options.TokensPerPeriod,
                    ReplenishmentPeriod = options.ReplenishmentPeriod,
                });
        });

        return new FailOpenRateLimiter(redisLimiter);
    }
}
