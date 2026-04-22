namespace PlatformWallet.Contracts.Commands;

public record HoldFunds(
    Guid CorrelationId,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    string Asset) : ITransactionMessage;

public record CaptureTransfer(
    Guid CorrelationId,
    Guid DebitAccountId,
    Guid CreditAccountId,
    decimal Amount,
    string Asset) : ITransactionMessage;

public record VoidHold(
    Guid CorrelationId,
    Guid DebitAccountId,
    decimal Amount,
    string Asset) : ITransactionMessage;

public record MintFunds(
    Guid CorrelationId,
    Guid CreditAccountId,
    decimal Amount,
    string Asset) : ITransactionMessage;

public record BurnFunds(
    Guid CorrelationId,
    Guid DebitAccountId,
    decimal Amount,
    string Asset) : ITransactionMessage;
