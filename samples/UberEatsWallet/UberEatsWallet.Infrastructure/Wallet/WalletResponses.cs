namespace UberEatsWallet.Infrastructure.Wallet;

// Shapes returned by the wallet edge. Deserialized with web defaults (camelCase, case-insensitive).
internal sealed record AcceptedResponse(Guid TransactionId);

internal sealed record BalanceResponse(Guid AccountId, string Asset, decimal Balance, decimal HeldAmount);

internal sealed record TransactionResponse(Guid TransactionId, string Status);

internal sealed record HistoryItemResponse(
    Guid Id,
    Guid TxId,
    string Asset,
    decimal AmountSigned,
    string EntryKind,
    string Phase,
    DateTimeOffset CreatedAt);

internal sealed record HistoryResponse(
    Guid AccountId,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<HistoryItemResponse> Items);
