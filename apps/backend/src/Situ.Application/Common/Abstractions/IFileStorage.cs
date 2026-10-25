namespace Situ.Application.Common.Abstractions;

/// <summary>
/// Stores report files. Implementations must not expose disk paths.
/// A later S3-compatible provider can replace <c>LocalFileStorage</c> without changing callers.
/// </summary>
public interface IFileStorage
{
    Task SaveAsync(string key, byte[] content, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);
}

public interface IMediaUrlSigner
{
    (long ExpiresUnix, string Signature) Sign(Guid mediaId, DateTimeOffset expiresAt);

    bool IsValid(Guid mediaId, long expiresUnix, string signature, DateTimeOffset now);
}
