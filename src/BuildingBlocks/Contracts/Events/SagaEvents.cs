namespace PlatformWallet.Contracts.Events;

public record TransactionHeld(Guid CorrelationId, Guid DebitAccountId, Guid CreditAccountId) : ITransactionMessage;

public record TransactionCaptured(Guid CorrelationId, Guid DebitAccountId, Guid CreditAccountId) : ITransactionMessage;

public record TransactionVoided(Guid CorrelationId, Guid DebitAccountId, Guid CreditAccountId) : ITransactionMessage;

public record TransactionFailed(Guid CorrelationId, string Reason) : ITransactionMessage;

public record TransactionMinted(Guid CorrelationId, Guid? DebitAccountId, Guid CreditAccountId) : ITransactionMessage;

public record TransactionBurned(Guid CorrelationId, Guid DebitAccountId, Guid CreditAccountId) : ITransactionMessage;
