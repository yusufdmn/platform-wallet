using PlatformWallet.BalanceQuery.Application.Queries;

namespace PlatformWallet.BalanceQuery.Api.Endpoints;

internal static class BalanceEndpoint
{
    private const string BalanceRoute = "/accounts/{accountId:guid}/balance";
    private const string ReadPolicy   = "ledger:read";

    public static IEndpointRouteBuilder MapBalanceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(BalanceRoute, HandleAsync)
            .RequireAuthorization(ReadPolicy);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        Guid                  accountId,
        IBalanceQueryService  queryService,
        CancellationToken     ct)
    {
        var balance = await queryService.GetBalanceAsync(accountId, ct);

        if (balance is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new
        {
            accountId  = balance.AccountId,
            asset      = balance.Asset,
            balance    = balance.Balance,
            heldAmount = balance.HeldAmount,
        });
    }
}
