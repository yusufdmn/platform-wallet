namespace PlatformWallet.WebhookDispatcher.Infrastructure.Configuration;

public sealed class WebhookOptions
{
    public string TargetUrl  { get; set; } = null!;
    public string HmacSecret { get; set; } = null!;
}
