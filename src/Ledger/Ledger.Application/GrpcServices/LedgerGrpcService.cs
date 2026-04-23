using System.Globalization;
using Grpc.Core;
using PlatformWallet.Grpc.Protos;
using PlatformWallet.Ledger.Application.Persistence;

namespace PlatformWallet.Ledger.Application.GrpcServices;

public sealed class LedgerGrpcService(IAccountQueries accountQueries) : LedgerReader.LedgerReaderBase
{
    public override async Task<GetAccountBalanceResponse> GetAccountBalance(
        GetAccountBalanceRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid account id."));
        }

        var dto = await accountQueries.GetBalanceAsync(accountId, context.CancellationToken);

        if (dto is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Account {accountId} not found."));
        }

        return new GetAccountBalanceResponse
        {
            AccountId  = dto.Id.ToString(),
            Asset      = dto.Asset,
            Balance    = dto.Balance.ToString("G", CultureInfo.InvariantCulture),
            HeldAmount = dto.HeldAmount.ToString("G", CultureInfo.InvariantCulture),
        };
    }

    public override Task<GetPostingsResponse> GetPostings(
        GetPostingsRequest request, ServerCallContext context) =>
        Task.FromResult(new GetPostingsResponse());
}
