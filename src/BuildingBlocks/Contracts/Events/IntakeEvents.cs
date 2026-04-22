namespace PlatformWallet.Contracts.Events;

public record TransactionSubmitted(
    Guid CorrelationId,
    string TransactionType,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    string Asset) : ITransactionMessage;

public record CaptureTransferRequested(Guid CorrelationId) : ITransactionMessage;

public record VoidRequested(Guid CorrelationId) : ITransactionMessage;
