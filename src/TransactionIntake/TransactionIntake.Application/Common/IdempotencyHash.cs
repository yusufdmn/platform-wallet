using System.Security.Cryptography;
using System.Text;

namespace PlatformWallet.TransactionIntake.Application.Common;

internal static class IdempotencyHash
{
    public static string Compute(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes);
    }
}
