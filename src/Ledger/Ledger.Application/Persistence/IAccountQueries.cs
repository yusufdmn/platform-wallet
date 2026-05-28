namespace PlatformWallet.Ledger.Application.Persistence;

public record AccountBalanceDto(Guid Id, string Asset, decimal Balance, decimal HeldAmount);

public record PostingHistoryItem(
    long           Id,
    Guid           TxId,
    Guid           AccountId,
    string         Asset,
    decimal        AmountSigned,
    string         EntryKind,
    string         Phase,
    DateTimeOffset CreatedAt);

public record PostingHistoryPage(IReadOnlyList<PostingHistoryItem> Items, int TotalCount);

public interface IAccountQueries
{
    Task<AccountBalanceDto?> GetBalanceAsync(Guid accountId, CancellationToken ct);

    Task<PostingHistoryPage> GetPostingsAsync(Guid accountId, int page, int pageSize, CancellationToken ct);
}
