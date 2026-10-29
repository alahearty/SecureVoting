using System.Security.Cryptography;
using System.Text;

namespace SecureVoting.Application.Security;

public static class TokenGenerator
{
    public static string Create(int byteCount = 32)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(byteCount, 16);
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
