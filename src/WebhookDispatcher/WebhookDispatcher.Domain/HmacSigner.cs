using System.Security.Cryptography;

namespace PlatformWallet.WebhookDispatcher.Domain;

public static class HmacSigner
{
    public static string Sign(ReadOnlySpan<byte> body, ReadOnlySpan<byte> key)
    {
        Span<byte> hash = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, body, hash);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
