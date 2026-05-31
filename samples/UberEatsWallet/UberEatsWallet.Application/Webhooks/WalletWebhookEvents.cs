namespace UberEatsWallet.Application.Webhooks;

/// <summary>The <c>event_type</c> values the Webhook Dispatcher sends (see WebhookEventTypes in the wallet).</summary>
public static class WalletWebhookEvents
{
    public const string Minted = "transaction.minted";
    public const string Burned = "transaction.burned";
    public const string Captured = "transaction.captured";
    public const string Voided = "transaction.voided";
    public const string Failed = "transaction.failed";
}
