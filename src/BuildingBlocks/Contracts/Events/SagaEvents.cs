namespace PlatformWallet.Contracts.Events;

public record TransactionCaptured(Guid CorrelationId) : ITransactionMessage;

public record TransactionVoided(Guid CorrelationId) : ITransactionMessage;

public record TransactionFailed(Guid CorrelationId, string Reason) : ITransactionMessage;

public record TransactionMinted(Guid CorrelationId) : ITransactionMessage;

public record TransactionBurned(Guid CorrelationId) : ITransactionMessage;
