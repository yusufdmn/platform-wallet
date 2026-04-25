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
    private const string CacheKeyPrefix   = "balance:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<AccountBalance?> GetBalanceAsync(Guid accountId, CancellationToken ct)
    {
        var cacheKey = BuildCacheKey(accountId);

        return await cache.GetOrSetAsync(
            cacheKey,
            async token => await FetchFromLedgerAsync(accountId, token),
            options => options.SetDuration(CacheDuration),
            token: ct);
    }

    private async Task<AccountBalance?> FetchFromLedgerAsync(Guid accountId, CancellationToken ct)
    {
        logger.LogDebug("Fetching balance from Ledger gRPC for account {AccountId}", accountId);

        var request = new GetAccountBalanceRequest { AccountId = accountId.ToString() };

        try
        {
            var response = await grpcClient.GetAccountBalanceAsync(request, cancellationToken: ct);
            return ParseResponse(response);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            logger.LogInformation("Account {AccountId} not found in Ledger", accountId);
            return null;
        }
    }

    private static AccountBalance? ParseResponse(GetAccountBalanceResponse response)
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

    private static string BuildCacheKey(Guid accountId) => $"{CacheKeyPrefix}{accountId}";
}
