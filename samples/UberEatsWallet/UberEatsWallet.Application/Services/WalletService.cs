using UberEatsWallet.Application.Abstractions;

namespace UberEatsWallet.Application.Services;

/// <summary>Cash-in / cash-out and read-only views over a wallet account.</summary>
public sealed class WalletService(IWalletGateway gateway)
{
    /// <summary>Deposit (mock card) → mint. Auto-creates the ledger account on first use.</summary>
    public Task<Guid> TopUpAsync(Guid accountId, decimal amount, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        return gateway.MintAsync(accountId, amount, Guid.NewGuid().ToString(), ct);
    }

    /// <summary>Withdraw to bank → burn.</summary>
    public Task<Guid> WithdrawAsync(Guid accountId, decimal amount, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        return gateway.BurnAsync(accountId, amount, Guid.NewGuid().ToString(), ct);
    }

    public Task<WalletBalance?> GetBalanceAsync(Guid accountId, CancellationToken ct) =>
        gateway.GetBalanceAsync(accountId, ct);

    public Task<WalletHistory> GetHistoryAsync(Guid accountId, int page, int pageSize, CancellationToken ct) =>
        gateway.GetHistoryAsync(accountId, page, pageSize, ct);
}
