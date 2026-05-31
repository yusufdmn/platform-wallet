namespace UberEatsWallet.Application.Abstractions;

/// <summary>
/// The one and only door to the Platform Wallet. Implemented in Infrastructure by a typed
/// HttpClient that attaches the bearer token, <c>api-version</c> header, and idempotency key.
/// Writes are async on the wallet side: each returns the wallet <c>transactionId</c> (== saga
/// <c>correlationId</c>), which the caller stores to later capture/void.
/// </summary>
public interface IWalletGateway
{
    Task<Guid> MintAsync(Guid accountId, decimal amount, string idempotencyKey, CancellationToken ct);

    Task<Guid> BurnAsync(Guid accountId, decimal amount, string idempotencyKey, CancellationToken ct);

    Task<Guid> TransferAsync(
        Guid debitAccountId,
        Guid creditAccountId,
        decimal amount,
        string idempotencyKey,
        CancellationToken ct);

    Task CaptureAsync(Guid correlationId, CancellationToken ct);

    Task VoidAsync(Guid correlationId, CancellationToken ct);

    Task<WalletBalance?> GetBalanceAsync(Guid accountId, CancellationToken ct);

    Task<WalletHistory> GetHistoryAsync(Guid accountId, int page, int pageSize, CancellationToken ct);

    /// <summary>Current intake status of a transaction (e.g. "Held", "Captured"), or null if unknown.</summary>
    Task<string?> GetTransactionStatusAsync(Guid transactionId, CancellationToken ct);
}
