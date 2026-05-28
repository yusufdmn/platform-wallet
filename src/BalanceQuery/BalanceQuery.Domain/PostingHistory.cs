namespace PlatformWallet.BalanceQuery.Domain;

public sealed record PostingHistoryEntry(
    long           Id,
    Guid           TxId,
    Guid           AccountId,
    string         Asset,
    decimal        AmountSigned,
    string         EntryKind,
    string         Phase,
    DateTimeOffset CreatedAt);

public sealed record PostingHistory(
    IReadOnlyList<PostingHistoryEntry> Items,
    int                                TotalCount,
    int                                Page,
    int                                PageSize);
