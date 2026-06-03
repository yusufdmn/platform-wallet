using UberEatsWallet.Application.Abstractions;

namespace UberEatsWallet.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
