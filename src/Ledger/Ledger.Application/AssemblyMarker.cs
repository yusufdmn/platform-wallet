namespace PlatformWallet.Ledger.Application;

/// <summary>
/// Assembly marker. Will host MassTransit IConsumer&lt;T&gt; implementations
/// (HoldFundsConsumer, CaptureTransferConsumer, VoidHoldConsumer,
/// MintFundsConsumer, BurnFundsConsumer) and gRPC service implementations.
/// </summary>
public interface IAssemblyMarker;
