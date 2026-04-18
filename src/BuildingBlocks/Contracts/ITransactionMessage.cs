namespace PlatformWallet.Contracts;

/// <summary>
/// Marker interface shared by every command/event exchanged between services
/// in TheMainPlan.md §0.5. Concrete records (HoldFunds, FundsHeld,
/// CaptureTransfer, TransferCaptured, VoidHold, HoldVoided, MintFunds,
/// FundsMinted, BurnFunds, FundsBurned, TransactionSubmitted,
/// TransactionCaptured, TransactionVoided, TransactionFailed,
/// TransactionMinted, TransactionBurned) will be added as this project grows.
/// Carries only the universal correlation id; payload shape is per-message.
/// </summary>
public interface ITransactionMessage
{
    Guid CorrelationId { get; }
}
