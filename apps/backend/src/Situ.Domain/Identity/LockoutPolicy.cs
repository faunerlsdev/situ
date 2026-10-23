namespace Situ.Domain.Identity;

/// <summary>Failed-login protection (RF-AUT-09). Values come from system settings; defaults are 5 attempts and 15 minutes.</summary>
public sealed record LockoutPolicy(int MaxFailedAttempts, TimeSpan LockoutDuration)
{
    public static readonly LockoutPolicy Default = new(5, TimeSpan.FromMinutes(15));
}
