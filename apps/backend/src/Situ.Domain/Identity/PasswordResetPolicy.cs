namespace Situ.Domain.Identity;

/// <summary>Lifetime of a password-reset link and how often a new one may be sent.</summary>
public static class PasswordResetPolicy
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromMinutes(1);
}
