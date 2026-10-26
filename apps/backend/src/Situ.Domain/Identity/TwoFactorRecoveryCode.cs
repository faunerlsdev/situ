using Situ.Domain.Common;

namespace Situ.Domain.Identity;

/// <summary>One-time backup code for staff TOTP. Only the hash is stored.</summary>
public sealed class TwoFactorRecoveryCode : AuditableEntity
{
    private TwoFactorRecoveryCode()
    {
        CodeHash = null!;
    }

    public Guid UserId { get; private set; }

    public string CodeHash { get; private set; }

    public DateTimeOffset? UsedAt { get; private set; }

    public bool IsUsed => UsedAt is not null;

    public static TwoFactorRecoveryCode Issue(Guid userId, string codeHash, DateTimeOffset now)
    {
        var code = new TwoFactorRecoveryCode
        {
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            CodeHash = Guard.NotEmpty(codeHash, nameof(codeHash), 300),
        };
        code.MarkCreated(now);
        return code;
    }

    public bool TryUse(DateTimeOffset now)
    {
        if (IsUsed)
        {
            return false;
        }

        UsedAt = now;
        MarkUpdated(now);
        return true;
    }
}
