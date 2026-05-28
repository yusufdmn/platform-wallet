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

    public override async Task<GetPostingsResponse> GetPostings(
        GetPostingsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid account id."));
        }

        var page = await accountQueries.GetPostingsAsync(
            accountId, request.Page, request.PageSize, context.CancellationToken);

        var response = new GetPostingsResponse { TotalCount = page.TotalCount };
        foreach (var item in page.Items)
        {
            response.Postings.Add(MapToDto(item));
        }
        return response;
    }

    private static PostingDto MapToDto(PostingHistoryItem item) => new()
    {
        Id           = item.Id.ToString(CultureInfo.InvariantCulture),
        TxId         = item.TxId.ToString(),
        AccountId    = item.AccountId.ToString(),
        Asset        = item.Asset,
        AmountSigned = item.AmountSigned.ToString("G", CultureInfo.InvariantCulture),
        EntryKind    = item.EntryKind,
        Phase        = item.Phase,
        CreatedAt    = item.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
    };
}
