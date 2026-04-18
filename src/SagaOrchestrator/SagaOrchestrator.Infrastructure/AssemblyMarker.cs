namespace PlatformWallet.SagaOrchestrator.Infrastructure;

/// <summary>
/// Assembly marker. Hosts SagaDbContext (transaction_saga_state + failed_messages +
/// MT inbox), EF configurations (row_version as concurrency token), migrations.
/// </summary>
public interface IAssemblyMarker;
