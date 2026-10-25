using System.Security.Cryptography;
using System.Text;
using Situ.Application.Common.Abstractions;

namespace Situ.Infrastructure.Reports;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string Root { get; set; } = "";

    public string SigningKey { get; set; } = "";
}

public sealed class LocalFileStorage(FileStorageOptions options) : IFileStorage
{
    public async Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Stream? stream = File.Exists(path) ? File.OpenRead(path) : null;
        return Task.FromResult(stream);
    }

    private string PathFor(string key)
    {
        var root = Path.GetFullPath(options.Root);
        var relative = key.Replace('\\', '/').TrimStart('/');
        if (relative.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }

        var path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid storage key.");
        }

        return path;
    }
}

public sealed class HmacMediaUrlSigner(FileStorageOptions options) : IMediaUrlSigner
{
    public (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt)
    {
        var unix = expiresAt.ToUnixTimeSeconds();
        return (unix, Signature(mediaId, unix));
    }

    public bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now)
    {
        if (expiresUnix < now.ToUnixTimeSeconds() || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expected = Signature(mediaId, expiresUnix);
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(signature);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private string Signature(Guid mediaId, long expiresUnix)
    {
        var key = Encoding.UTF8.GetBytes(options.SigningKey);
        var payload = Encoding.UTF8.GetBytes($"{mediaId:N}.{expiresUnix}");
        return Convert.ToHexString(HMACSHA256.HashData(key, payload));
    }
}
