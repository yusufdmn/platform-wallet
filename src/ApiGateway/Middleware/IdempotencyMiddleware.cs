using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace PlatformWallet.ApiGateway.Yarp.Middleware;

public sealed class IdempotencyMiddleware(RequestDelegate next, IDistributedCache cache)
{
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string CacheKeyPrefix    = "gw:idempotency:";

    private static readonly string[] MutableMethods = ["POST", "PUT", "PATCH"];

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!MutableMethods.Contains(ctx.Request.Method, StringComparer.OrdinalIgnoreCase)
            || !ctx.Request.Headers.TryGetValue(IdempotencyHeader, out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues))
        {
            await next(ctx);
            return;
        }

        var callerId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? ctx.User.FindFirstValue("sub")
                    ?? "anonymous";

        var cacheKey = $"{CacheKeyPrefix}{callerId}:{ctx.Request.Method}:{ctx.Request.Path}:{keyValues}";
        var cached   = await cache.GetStringAsync(cacheKey, ctx.RequestAborted);

        if (cached is not null)
        {
            try
            {
                var entry = JsonSerializer.Deserialize<IdempotencyEntry>(cached);
                if (entry is not null)
                {
                    ctx.Response.StatusCode = entry.StatusCode;
                    if (!string.IsNullOrEmpty(entry.ContentType))
                    {
                        ctx.Response.ContentType = entry.ContentType;
                    }
                    await ctx.Response.Body.WriteAsync(entry.BodyBytes, ctx.RequestAborted);
                    return;
                }
            }
            catch (JsonException)
            {
                await cache.RemoveAsync(cacheKey, ctx.RequestAborted);
            }
        }

        var originalBody = ctx.Response.Body;
        await using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;

        try
        {
            await next(ctx);

            if (ctx.Response.StatusCode is >= 200 and < 300)
            {
                var entry = new IdempotencyEntry(
                    ctx.Response.StatusCode,
                    ctx.Response.ContentType ?? string.Empty,
                    buffer.ToArray());

                await cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(entry),
                    CacheOptions,
                    ctx.RequestAborted);
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody, ctx.RequestAborted);
        }
        finally
        {
            ctx.Response.Body = originalBody;
        }
    }

    private sealed record IdempotencyEntry(int StatusCode, string ContentType, byte[] BodyBytes);
}
