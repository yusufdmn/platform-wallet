namespace UberEatsWallet.Infrastructure.Wallet;

/// <summary>Raised when the wallet edge returns a non-success HTTP status.</summary>
public sealed class WalletGatewayException(int statusCode, string responseBody)
    : Exception($"Wallet returned HTTP {statusCode}: {responseBody}")
{
    public int StatusCode { get; } = statusCode;
}
