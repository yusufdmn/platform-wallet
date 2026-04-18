namespace PlatformWallet.BalanceQuery.Infrastructure;

/// <summary>
/// Assembly marker. Hosts FusionCache configuration (L1 + L2 Redis backplane,
/// single-flight, fail-safe, 80% eager refresh), gRPC client to Ledger,
/// Dapper IDbConnection factory for postings history reads.
/// </summary>
public interface IAssemblyMarker;
