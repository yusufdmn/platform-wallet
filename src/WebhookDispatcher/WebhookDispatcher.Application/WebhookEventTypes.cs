using PlatformWallet.Contracts.Events;

namespace PlatformWallet.WebhookDispatcher.Application;

public static class WebhookEventTypes
{
    public const string TransactionMinted   = "transaction.minted";
    public const string TransactionBurned   = "transaction.burned";
    public const string TransactionCaptured = "transaction.captured";
    public const string TransactionVoided   = "transaction.voided";
    public const string TransactionFailed   = "transaction.failed";

    private static readonly Dictionary<Type, string> Map = new()
    {
        [typeof(TransactionMinted)]   = TransactionMinted,
        [typeof(TransactionBurned)]   = TransactionBurned,
        [typeof(TransactionCaptured)] = TransactionCaptured,
        [typeof(TransactionVoided)]   = TransactionVoided,
        [typeof(TransactionFailed)]   = TransactionFailed,
    };

    public static string Resolve<TEvent>() =>
        Map.TryGetValue(typeof(TEvent), out var name)
            ? name
            : typeof(TEvent).Name;
}
