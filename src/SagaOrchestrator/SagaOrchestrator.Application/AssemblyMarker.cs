namespace PlatformWallet.SagaOrchestrator.Application;

/// <summary>
/// Assembly marker. Hosts fault consumers IConsumer&lt;Fault&lt;HoldFunds&gt;&gt;,
/// IConsumer&lt;Fault&lt;CaptureTransfer&gt;&gt;, IConsumer&lt;Fault&lt;VoidHold&gt;&gt;,
/// IConsumer&lt;Fault&lt;MintFunds&gt;&gt;, IConsumer&lt;Fault&lt;BurnFunds&gt;&gt; that persist
/// to failed_messages. Required by TheMainPlan.md §0.8.
/// </summary>
public interface IAssemblyMarker;
