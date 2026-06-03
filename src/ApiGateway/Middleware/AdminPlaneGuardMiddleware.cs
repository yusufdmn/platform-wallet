namespace PlatformWallet.ApiGateway.Yarp.Middleware;

/// <summary>
/// Confines the admin plane (Ops Console + <c>/admin</c> API) to a dedicated internal
/// Kestrel listener. Requests for those paths that arrive on any other port are answered
/// with 404 — not 401/403 — so the public listener gives no hint the admin plane exists.
/// The internal port is read from configuration (<see cref="InternalPortKey"/>); it differs
/// between dev and container, so it is never hardcoded here.
/// </summary>
public sealed class AdminPlaneGuardMiddleware
{
    private const string ConsolePrefix  = "/console";
    private const string AdminPrefix    = "/admin";
    private const string InternalPortKey = "OpsConsole:InternalListenerPort";

    private readonly RequestDelegate _next;
    private readonly int _internalListenerPort;

    public AdminPlaneGuardMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _internalListenerPort = config.GetValue<int?>(InternalPortKey)
            ?? throw new InvalidOperationException($"{InternalPortKey} is required.");
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (IsAdminPlane(ctx.Request.Path) && ctx.Connection.LocalPort != _internalListenerPort)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(ctx);
    }

    private static bool IsAdminPlane(PathString path) =>
        path.StartsWithSegments(ConsolePrefix) || path.StartsWithSegments(AdminPrefix);
}
