using System.Threading.RateLimiting;
using StackExchange.Redis;

namespace PlatformWallet.ApiGateway.Yarp.Infrastructure.RateLimiting;

/// <summary>
/// Wraps a Redis-backed <see cref="PartitionedRateLimiter{HttpContext}"/> and fails open:
/// if Redis is unreachable, the request is allowed through (and a warning is logged) rather
/// than surfacing as a 500. Availability is favoured over strict enforcement, which is the
/// right trade-off for an edge gateway whose Redis is a shared, best-effort dependency.
/// </summary>
internal sealed class FailOpenRateLimiter(PartitionedRateLimiter<HttpContext> inner)
    : PartitionedRateLimiter<HttpContext>
{
    private const string LoggerCategory = "PlatformWallet.ApiGateway.Yarp.RateLimiting";

    public override RateLimiterStatistics? GetStatistics(HttpContext resource)
    {
        try
        {
            return inner.GetStatistics(resource);
        }
        catch (RedisException)
        {
            return null;
        }
    }

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(
        HttpContext resource, int permitCount, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.AcquireAsync(resource, permitCount, cancellationToken);
        }
        catch (RedisException ex)
        {
            LogFailOpen(resource, ex);
            return AllowedLease.Instance;
        }
    }

    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount)
    {
        try
        {
            return inner.AttemptAcquire(resource, permitCount);
        }
        catch (RedisException ex)
        {
            LogFailOpen(resource, ex);
            return AllowedLease.Instance;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override ValueTask DisposeAsyncCore() => inner.DisposeAsync();

    private static void LogFailOpen(HttpContext resource, Exception ex)
    {
        resource.RequestServices
            .GetService<ILoggerFactory>()?
            .CreateLogger(LoggerCategory)
            .LogWarning(ex,
                "Rate-limit store unreachable; allowing request to {Path} (fail-open)",
                resource.Request.Path);
    }

    private sealed class AllowedLease : RateLimitLease
    {
        public static readonly AllowedLease Instance = new();

        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames => Array.Empty<string>();

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing) => base.Dispose(disposing);
    }
}
