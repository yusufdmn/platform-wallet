namespace PlatformWallet.SagaOrchestrator.Domain;

/// <summary>
/// Assembly marker. Hosts TransactionSagaState + TransactionSagaStateMachine
/// (Submitted -> Held -> Captured | Voided | Compensating -> Failed).
/// Reviewed by `masstransit-saga-reviewer` subagent.
/// </summary>
public interface IAssemblyMarker;
