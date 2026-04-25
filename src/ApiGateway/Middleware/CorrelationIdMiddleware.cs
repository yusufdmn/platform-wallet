using System.Diagnostics;

namespace PlatformWallet.ApiGateway.Yarp.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var traceId = Activity.Current?.TraceId.ToString();

        if (!string.IsNullOrEmpty(traceId))
        {
            ctx.Request.Headers[CorrelationIdHeader] = traceId;
        }

        await next(ctx);
    }
}
