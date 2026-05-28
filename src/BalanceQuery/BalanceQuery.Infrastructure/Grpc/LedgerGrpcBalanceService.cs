using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using PlatformWallet.BalanceQuery.Application.Queries;
using PlatformWallet.BalanceQuery.Domain;
using PlatformWallet.Grpc.Protos;
using ZiggyCreatures.Caching.Fusion;

namespace PlatformWallet.BalanceQuery.Infrastructure.Grpc;

internal sealed class LedgerGrpcBalanceService(
    LedgerReader.LedgerReaderClient grpcClient,
    IFusionCache                    cache,
    ILogger<LedgerGrpcBalanceService> logger) : IBalanceQueryService
{
    private const string BalanceCacheKeyPrefix = "balance:";
    private const string HistoryCacheKeyPrefix = "history:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<AccountBalance?> GetBalanceAsync(Guid accountId, CancellationToken ct)
    {
        var cacheKey = BuildBalanceCacheKey(accountId);

        return await cache.GetOrSetAsync<AccountBalance?>(
            cacheKey,
            async (ctx, token) =>
            {
                var result = await FetchBalanceFromLedgerAsync(accountId, token);
                if (result is null)
                {
                    ctx.Options.SkipMemoryCache      = true;
                    ctx.Options.SkipDistributedCache = true;
                }
                return result;
            },
            options => options.SetDuration(CacheDuration),
            token: ct);
    }

    public async Task<PostingHistory> GetHistoryAsync(
        Guid              accountId,
        int               page,
        int               pageSize,
        CancellationToken ct)
    {
        var cacheKey = BuildHistoryCacheKey(accountId, page, pageSize);

        return await cache.GetOrSetAsync<PostingHistory>(
            cacheKey,
            (_, token) => FetchHistoryFromLedgerAsync(accountId, page, pageSize, token),
            options => options.SetDuration(CacheDuration),
            token: ct);
    }

    private async Task<AccountBalance?> FetchBalanceFromLedgerAsync(Guid accountId, CancellationToken ct)
    {
        logger.LogDebug("Fetching balance from Ledger gRPC for account {AccountId}", accountId);

        var request = new GetAccountBalanceRequest { AccountId = accountId.ToString() };

        try
        {
            var response = await grpcClient.GetAccountBalanceAsync(request, cancellationToken: ct);
            return ParseBalanceResponse(response);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogInformation("Account {AccountId} not found in Ledger", accountId);
            return null;
        }
    }

    private async Task<PostingHistory> FetchHistoryFromLedgerAsync(
        Guid              accountId,
        int               page,
        int               pageSize,
        CancellationToken ct)
    {
        logger.LogDebug(
            "Fetching postings history from Ledger gRPC for account {AccountId} page {Page} size {PageSize}",
            accountId, page, pageSize);

        var request = new GetPostingsRequest
        {
            AccountId = accountId.ToString(),
            Page      = page,
            PageSize  = pageSize,
        };

        try
        {
            var response = await grpcClient.GetPostingsAsync(request, cancellationToken: ct);
            return ParseHistoryResponse(response, page, pageSize);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogInformation("Account {AccountId} not found in Ledger", accountId);
            return EmptyHistory(page, pageSize);
        }
    }

    private static AccountBalance? ParseBalanceResponse(GetAccountBalanceResponse response)
    {
        if (string.IsNullOrEmpty(response.AccountId))
        {
            return null;
        }

        return new AccountBalance(
            AccountId:  Guid.Parse(response.AccountId),
            Asset:      response.Asset,
            Balance:    decimal.Parse(response.Balance,    CultureInfo.InvariantCulture),
            HeldAmount: decimal.Parse(response.HeldAmount, CultureInfo.InvariantCulture));
    }

    private static PostingHistory ParseHistoryResponse(GetPostingsResponse response, int page, int pageSize)
    {
        var items = response.Postings.Select(MapToEntry).ToList();
        return new PostingHistory(items, response.TotalCount, page, pageSize);
    }

    private static PostingHistoryEntry MapToEntry(PostingDto dto) => new(
        Id:           long.Parse(dto.Id, CultureInfo.InvariantCulture),
        TxId:         Guid.Parse(dto.TxId),
        AccountId:    Guid.Parse(dto.AccountId),
        Asset:        dto.Asset,
        AmountSigned: decimal.Parse(dto.AmountSigned, CultureInfo.InvariantCulture),
        EntryKind:    dto.EntryKind,
        Phase:        dto.Phase,
        CreatedAt:    DateTimeOffset.Parse(dto.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

    private static PostingHistory EmptyHistory(int page, int pageSize) =>
        new(Array.Empty<PostingHistoryEntry>(), TotalCount: 0, Page: page, PageSize: pageSize);

    private static string BuildBalanceCacheKey(Guid accountId) =>
        $"{BalanceCacheKeyPrefix}{accountId}";

    private static string BuildHistoryCacheKey(Guid accountId, int page, int pageSize) =>
        $"{HistoryCacheKeyPrefix}{accountId}:{page}:{pageSize}";
}
