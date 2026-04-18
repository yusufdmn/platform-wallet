namespace PlatformWallet.Ledger.Infrastructure;

/// <summary>
/// Assembly marker. Will host LedgerDbContext (EF Core writes for accounts
/// + postings), EF configurations, migrations, IDbConnection factory for
/// Dapper reads, and DI registration extension methods.
/// </summary>
public interface IAssemblyMarker;
