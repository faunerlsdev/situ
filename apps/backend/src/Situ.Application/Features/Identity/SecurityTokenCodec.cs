using System.Security.Cryptography;
using System.Text;

namespace Situ.Application.Features.Identity;

internal static class SecurityTokenCodec
{
    public static string CreatePlain() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    public static string Hash(string plainToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(hash);
    }
}
