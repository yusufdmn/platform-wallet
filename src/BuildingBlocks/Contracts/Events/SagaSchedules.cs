namespace PlatformWallet.Contracts.Events;

public record HoldExpired(Guid CorrelationId) : ITransactionMessage;
