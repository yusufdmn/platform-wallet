namespace UberEatsWallet.Application.Abstractions;

/// <summary>Commits pending changes to the app's own store (orders/catalog). The ledger is separate.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
