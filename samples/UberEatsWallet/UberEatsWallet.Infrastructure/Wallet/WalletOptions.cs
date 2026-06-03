namespace UberEatsWallet.Infrastructure.Wallet;

/// <summary>Connection + auth settings for talking to the Platform Wallet edge. Bound from the sample's .env.</summary>
public sealed class WalletOptions
{
    public string GatewayUrl { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = "1";
    public string Asset { get; set; } = "USD";
    public string TokenUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
}
