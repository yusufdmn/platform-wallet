namespace UberEatsWallet.Application.Abstractions;

/// <summary>A wallet account's current balance, as read back from the ledger (never stored locally).</summary>
public sealed record WalletBalance(Guid AccountId, string Asset, decimal Balance, decimal HeldAmount);

/// <summary>One posting in an account's ledger history.</summary>
public sealed record WalletHistoryEntry(
    Guid Id,
    Guid TxId,
    string Asset,
    decimal AmountSigned,
    string EntryKind,
    string Phase,
    DateTimeOffset CreatedAt);

/// <summary>A page of an account's ledger history.</summary>
public sealed record WalletHistory(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<WalletHistoryEntry> Items);
