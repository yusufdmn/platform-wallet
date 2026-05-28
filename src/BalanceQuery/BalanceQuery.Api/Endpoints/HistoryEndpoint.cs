using Microsoft.AspNetCore.Mvc;
using PlatformWallet.BalanceQuery.Application.Queries;

namespace PlatformWallet.BalanceQuery.Api.Endpoints;

internal static class HistoryEndpoint
{
    private const string HistoryRoute    = "/accounts/{accountId:guid}/history";
    private const string ReadPolicy      = "ledger:read";
    private const int    DefaultPageSize = 50;
    private const int    MaxPageSize     = 200;
    private const int    MinPage         = 1;

    public static IEndpointRouteBuilder MapHistoryEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(HistoryRoute, HandleAsync)
            .RequireAuthorization(ReadPolicy);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        Guid                            accountId,
        [FromQuery] int?                page,
        [FromQuery] int?                pageSize,
        IBalanceQueryService            queryService,
        CancellationToken               ct)
    {
        var clampedPage     = page is null or < MinPage ? MinPage         : page.Value;
        var clampedPageSize = pageSize switch
        {
            null or <= 0    => DefaultPageSize,
            > MaxPageSize   => MaxPageSize,
            _               => pageSize.Value,
        };

        var history = await queryService.GetHistoryAsync(accountId, clampedPage, clampedPageSize, ct);

        return Results.Ok(new
        {
            accountId  = accountId,
            page       = history.Page,
            pageSize   = history.PageSize,
            totalCount = history.TotalCount,
            items      = history.Items.Select(i => new
            {
                id           = i.Id,
                txId         = i.TxId,
                asset        = i.Asset,
                amountSigned = i.AmountSigned,
                entryKind    = i.EntryKind,
                phase        = i.Phase,
                createdAt    = i.CreatedAt,
            }),
        });
    }
}
