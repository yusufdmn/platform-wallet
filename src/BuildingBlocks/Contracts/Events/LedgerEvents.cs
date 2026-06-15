namespace PlatformWallet.Contracts.Events;

public record FundsHeld(Guid CorrelationId) : ITransactionMessage;

public record HoldFailed(Guid CorrelationId, string Reason) : ITransactionMessage;

public record TransferCaptured(Guid CorrelationId) : ITransactionMessage;

public record CaptureFailed(Guid CorrelationId, string Reason) : ITransactionMessage;

public record HoldVoided(Guid CorrelationId) : ITransactionMessage;

public record VoidFailed(Guid CorrelationId, string Reason) : ITransactionMessage;
