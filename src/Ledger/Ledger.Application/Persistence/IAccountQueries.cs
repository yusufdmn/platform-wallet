namespace PlatformWallet.Ledger.Application.Persistence;

public record AccountBalanceDto(Guid Id, string Asset, decimal Balance, decimal HeldAmount);

public interface IAccountQueries
{
    Task<AccountBalanceDto?> GetBalanceAsync(Guid accountId, CancellationToken ct);
}
