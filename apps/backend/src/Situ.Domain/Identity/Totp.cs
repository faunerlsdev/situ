using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Situ.Domain.Identity;

/// <summary>RFC 6238 TOTP: HMAC-SHA1, 6 digits, 30 seconds, one step of tolerance.</summary>
public static class Totp
{
    public const int Digits = 6;
    public const int StepSeconds = 30;
    public const int RecoveryCodeCount = 10;
    public const int MaxFailedAttempts = 5;

    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public readonly record struct CheckResult(bool Accepted, bool Reused, long Step);

    public static string GenerateSecret() => Encode(RandomNumberGenerator.GetBytes(20));

    public static string Code(string base32Secret, DateTimeOffset now) =>
        Compute(Decode(base32Secret), now.ToUnixTimeSeconds() / StepSeconds);

    public static CheckResult Check(string base32Secret, string code, DateTimeOffset now, long? lastAcceptedStep)
    {
        var normalized = new string((code ?? "").Where(char.IsDigit).ToArray());
        if (normalized.Length != Digits)
        {
            return default;
        }

        byte[] key;
        try
        {
            key = Decode(base32Secret);
        }
        catch (FormatException)
        {
            return default;
        }

        var step = now.ToUnixTimeSeconds() / StepSeconds;
        CheckResult? reused = null;
        for (var delta = -1; delta <= 1; delta++)
        {
            var candidate = step + delta;
            if (candidate < 0 || Compute(key, candidate) != normalized)
            {
                continue;
            }

            if (lastAcceptedStep is null || candidate > lastAcceptedStep)
            {
                return new CheckResult(true, false, candidate);
            }

            reused = new CheckResult(false, true, candidate);
        }

        return reused ?? default;
    }

    public static string OtpAuthUri(string base32Secret, string accountLabel)
    {
        var label = Uri.EscapeDataString("SITU:" + accountLabel);
        var secret = Uri.EscapeDataString(base32Secret);
        var issuer = Uri.EscapeDataString("SITU");
        return $"otpauth://totp/{label}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits={Digits}&period={StepSeconds}";
    }

    public static string CreateRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[9];
        var bytes = RandomNumberGenerator.GetBytes(8);
        for (var index = 0; index < 8; index++)
        {
            var slot = index < 4 ? index : index + 1;
            chars[slot] = alphabet[bytes[index] % alphabet.Length];
        }

        chars[4] = '-';
        return new string(chars);
    }

    public static string NormalizeRecoveryCode(string code) =>
        new string((code ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private static string Compute(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    public static string Encode(ReadOnlySpan<byte> data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        foreach (var item in data)
        {
            value = (value << 8) | item;
            bits += 8;
            while (bits >= 5)
            {
                output.Append(alphabet[(value >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        if (bits > 0)
        {
            output.Append(alphabet[(value << (5 - bits)) & 31]);
        }

        return output.ToString();
    }

    private static byte[] Decode(string secret)
    {
        var cleaned = secret.Trim().TrimEnd('=').ToUpperInvariant();
        var bits = 0;
        var value = 0;
        var output = new List<byte>(cleaned.Length * 5 / 8);
        foreach (var item in cleaned)
        {
            var index = item is >= 'A' and <= 'Z' ? item - 'A' : item is >= '2' and <= '7' ? item - '2' + 26 : -1;
            if (index < 0)
            {
                throw new FormatException("The TOTP secret is not Base32.");
            }

            value = (value << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        return output.ToArray();
    }
}
