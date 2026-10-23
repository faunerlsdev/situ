namespace Situ.Domain.Identity;

/// <summary>Lifetime of an e-mail confirmation link and how often a new one may be sent.</summary>
public static class EmailConfirmationPolicy
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public static readonly TimeSpan MinimumResendInterval = TimeSpan.FromMinutes(1);
}
