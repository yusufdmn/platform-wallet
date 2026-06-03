using System.Security.Cryptography;
using System.Text;

namespace UberEatsWallet.Domain;

/// <summary>
/// Verifies the wallet's <c>X-Signature</c> header. The dispatcher signs the raw body as
/// <c>sha256=&lt;lowercase-hex of HMAC-SHA256(body, secret)&gt;</c> with no timestamp, so the
/// signature is stable across retries and we can verify it on any delivery attempt.
/// </summary>
public static class WebhookSignatureVerifier
{
    private const string Prefix = "sha256=";

    public static bool IsValid(ReadOnlySpan<byte> body, string secret, string? signatureHeader)
    {
        ArgumentException.ThrowIfNullOrEmpty(secret);

        if (string.IsNullOrEmpty(signatureHeader) ||
            !signatureHeader.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var providedHex = signatureHeader.AsSpan(Prefix.Length);
        Span<byte> provided = stackalloc byte[HMACSHA256.HashSizeInBytes];
        if (!TryParseHex(providedHex, provided))
        {
            return false;
        }

        Span<byte> computed = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body, computed);

        return CryptographicOperations.FixedTimeEquals(computed, provided);
    }

    private static bool TryParseHex(ReadOnlySpan<char> hex, Span<byte> destination)
    {
        if (hex.Length != destination.Length * 2)
        {
            return false;
        }

        for (var i = 0; i < destination.Length; i++)
        {
            var high = FromHexNibble(hex[i * 2]);
            var low = FromHexNibble(hex[(i * 2) + 1]);
            if (high < 0 || low < 0)
            {
                return false;
            }

            destination[i] = (byte)((high << 4) | low);
        }

        return true;
    }

    private static int FromHexNibble(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1,
    };
}
