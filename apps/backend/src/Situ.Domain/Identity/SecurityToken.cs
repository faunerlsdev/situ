using Situ.Domain.Common;

namespace Situ.Domain.Identity;

/// <summary>
/// Single-use, expiring token for e-mail confirmation (RF-AUT-07) and password recovery (RF-AUT-08).
/// Only the hash of the token is stored; the plain value travels in the e-mailed link.
/// </summary>
public sealed class SecurityToken : AuditableEntity
{
    private SecurityToken()
    {
        TokenHash = null!;
    }

    public Guid UserId { get; private set; }
    public SecurityTokenPurpose Purpose { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static SecurityToken Issue(
        Guid userId,
        SecurityTokenPurpose purpose,
        string tokenHash,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        Guard.That(lifetime > TimeSpan.Zero, "security_token.invalid_lifetime", "Token lifetime must be positive.");

        var token = new SecurityToken
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Purpose = purpose,
            TokenHash = Guard.NotEmpty(tokenHash, nameof(tokenHash)),
            ExpiresAt = now.Add(lifetime),
        };
        token.MarkCreated(now);
        return token;
    }

    public bool IsValid(DateTimeOffset now) => ConsumedAt is null && ExpiresAt > now;

    /// <summary>Marks the token as used. Throws if it was already used or has expired.</summary>
    public void Consume(DateTimeOffset now)
    {
        Guard.That(ConsumedAt is null, "security_token.already_used", "This link was already used.");
        Guard.That(ExpiresAt > now, "security_token.expired", "This link has expired.");
        ConsumedAt = now;
    }

    /// <summary>Invalidates the token without using it (e.g. a newer one was issued).</summary>
    public void Invalidate(DateTimeOffset now) => ConsumedAt ??= now;
}
