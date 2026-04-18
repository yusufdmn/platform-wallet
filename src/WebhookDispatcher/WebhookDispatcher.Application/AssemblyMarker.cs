namespace PlatformWallet.WebhookDispatcher.Application;

/// <summary>
/// Assembly marker. Hosts TransactionTerminalEventConsumer (captures, voids,
/// failures, mints, burns), Polly pipeline factory, and the manual replay
/// command handler for /admin/webhooks/{id}/retry.
/// </summary>
public interface IAssemblyMarker;
