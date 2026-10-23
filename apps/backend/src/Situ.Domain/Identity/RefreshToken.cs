using Situ.Domain.Common;

namespace Situ.Domain.Identity;

/// <summary>Rotating refresh token bound to a device. Reuse of a rotated token signals theft and revokes the chain.</summary>
public sealed class RefreshToken : AuditableEntity
{
    private RefreshToken()
    {
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }
    public Guid UserDeviceId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public static RefreshToken Issue(Guid userId, Guid userDeviceId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        var token = new RefreshToken
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            UserDeviceId = Guard.NotEmpty(userDeviceId, nameof(userDeviceId)),
            TokenHash = Guard.NotEmpty(tokenHash, nameof(tokenHash)),
            ExpiresAt = now.Add(lifetime),
        };
        token.MarkCreated(now);
        return token;
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>Revokes this token and records which one replaced it.</summary>
    public void RotateTo(Guid newTokenId, DateTimeOffset now)
    {
        RevokedAt ??= now;
        ReplacedByTokenId = newTokenId;
    }
}
