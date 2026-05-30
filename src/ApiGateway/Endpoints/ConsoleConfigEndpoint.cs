namespace PlatformWallet.ApiGateway.Yarp.Endpoints;

public static class ConsoleConfigEndpoint
{
    private const string DefaultClientId    = "ops-console";
    private const string DefaultScope       = "openid ledger:admin";
    private const string CallbackPath       = "/console/callback.html";

    private const string AuthorityKey       = "KEYCLOAK_AUTHORITY";
    private const string ClientIdKey        = "OPS_CONSOLE_CLIENT_ID";
    private const string ScopeKey           = "OPS_CONSOLE_SCOPE";
    private const string RedirectUriKey     = "OPS_CONSOLE_REDIRECT_URI";

    public static void MapConsoleConfigEndpoint(this WebApplication app)
    {
        app.MapGet("/console/config.json", GetConfig).AllowAnonymous();
    }

    private static IResult GetConfig(IConfiguration config, HttpContext ctx)
    {
        var authority = config[AuthorityKey];
        if (string.IsNullOrWhiteSpace(authority))
        {
            return Results.Problem(
                title:      "Ops console config unavailable",
                detail:     $"{AuthorityKey} is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var redirectUri = config[RedirectUriKey];
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            var req = ctx.Request;
            redirectUri = $"{req.Scheme}://{req.Host}{CallbackPath}";
        }

        return Results.Json(new
        {
            keycloakAuthority = authority,
            clientId          = config[ClientIdKey] ?? DefaultClientId,
            scope             = config[ScopeKey] ?? DefaultScope,
            redirectUri,
        });
    }
}
